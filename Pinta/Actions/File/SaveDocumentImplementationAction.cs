// 
// SaveDocumentImplmentationAction.cs
//  
// Author:
//       Jonathan Pobst <monkey@jpobst.com>
// 
// Copyright (c) 2010 Jonathan Pobst
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Linq;
using Gtk;
using Mono.Unix;
using Pinta.Core;
using System.IO;
using System.Collections.Generic;

namespace Pinta.Actions
{
	class SaveDocumentImplmentationAction : IActionHandler
	{
		private const string markup = "<span weight=\"bold\" size=\"larger\">{0}</span>\n\n{1}";

		#region IActionHandler Members
		public void Initialize ()
		{
			PintaCore.Actions.File.SaveDocument += Activated;
		}

		public void Uninitialize ()
		{
			PintaCore.Actions.File.SaveDocument -= Activated;
		}
		#endregion

		private void Activated (object sender, DocumentCancelEventArgs e)
		{
			// Prompt for a new filename for "Save As", or a document that hasn't been saved before
			if (e.SaveAs || !e.Document.HasFile)
			{
				e.Cancel = !SaveFileAs (e.Document);
			}
			else
			{
				// Document hasn't changed, don't re-save it
				if (!e.Document.IsDirty)
					return;

				// If the document already has a filename, just re-save it
				e.Cancel = !SaveFile (e.Document, null, null, PintaCore.Chrome.MainWindow);
			}
		}

		// This is actually both for "Save As" and saving a file that never
		// been saved before.  Either way, we need to prompt for a filename.
		private bool SaveFileAs (Document document)
		{
			var fcd = new FileChooserDialog (Mono.Unix.Catalog.GetString ("Save Image File"),
									       PintaCore.Chrome.MainWindow,
									       FileChooserAction.Save,
									       Gtk.Stock.Cancel,
									       Gtk.ResponseType.Cancel,
									       Gtk.Stock.Save, Gtk.ResponseType.Ok);

			fcd.DoOverwriteConfirmation = true;
            fcd.SetCurrentFolder (PintaCore.System.GetDialogDirectory ());
			fcd.AlternativeButtonOrder = new int[] { (int)ResponseType.Ok, (int)ResponseType.Cancel };

			bool hasFile = document.HasFile;

			if (hasFile)
				fcd.SetFilename (document.PathAndFileName);
			else {
				// Append default extension (e.g., ".png") for new files
				var defaultFormat = PintaCore.System.ImageFormats.GetDefaultSaveFormat();
				fcd.CurrentName = "." + defaultFormat.Extensions.First();
			}

			Dictionary<FileFilter, FormatDescriptor> filetypes = new Dictionary<FileFilter, FormatDescriptor> ();

			// Add all the formats we support to the save dialog
			foreach (var format in PintaCore.System.ImageFormats.Formats) {
				if (!format.IsReadOnly ()) {
					fcd.AddFilter (format.Filter);
					filetypes.Add (format.Filter, format);

					// Set the filter to anything we found
					// We want to ensure that *something* is selected in the filetype
					fcd.Filter = format.Filter;
				}
			}

			// If we already have a format, set it to the default.
			// If not, default to png
			FormatDescriptor formatDesc = hasFile
				? PintaCore.System.ImageFormats.GetFormatByFile(document.Filename)
				: null;

			if (formatDesc == null) {
				formatDesc = PintaCore.System.ImageFormats.GetDefaultSaveFormat ();
			}

			fcd.Filter = formatDesc.Filter;

            fcd.AddNotification("filter", this.OnFilterChanged);

			// Replace GTK's ConfirmOverwrite with our own, for UI consistency
			fcd.ConfirmOverwrite += (sender, args) => {
				args.RetVal = this.ConfirmOverwrite(fcd, fcd.Filename)
					? FileChooserConfirmation.AcceptFilename
					: FileChooserConfirmation.SelectAgain;
			};

			while (fcd.Run () == (int)Gtk.ResponseType.Ok) {
				string file = fcd.Filename;
				bool fileExists = File.Exists(file);

				if (string.IsNullOrEmpty (Path.GetExtension (file))) {
					// No extension; add one from the format descriptor.
					file = $"{file}.{filetypes[fcd.Filter].Extensions[0]}";
					fcd.CurrentName = Path.GetFileName (file);

					// We also need to display an overwrite confirmation message manually,
					// because MessageDialog won't do this for us in this case.
					if (fileExists && !ConfirmOverwrite (fcd, file))
						continue;
				}

				// Always follow the extension rather than the file type drop down
				// ie: if the user chooses to save a "jpeg" as "foo.png", we are going
				// to assume they just didn't update the dropdown and really want png
				var formatType = PintaCore.System.ImageFormats.GetFormatByFile(file);
				if (formatType != null)
					formatDesc = formatType;

				PintaCore.System.LastDialogDirectory = fcd.CurrentFolder;

				// If saving the file failed or was cancelled, let the user select
				// a different file type.
				if (!SaveFile (document, file, formatDesc, fcd))
					continue;

				// The user is saving the Document to a new file, so technically it
				// hasn't been saved to its associated file in this session.
				document.HasBeenSavedInSession = false;

				// Workaround for Gtk FileChooserDialog failing to assign proper mimetype
				if (!fileExists) {
					RecentManager.Default.RemoveItem (fcd.Uri);
					AddRecentFile (fcd.Uri, GetMimeTypeForFile (file));
				}
				PintaCore.System.ImageFormats.SetDefaultFormat (Path.GetExtension (file));

				document.HasFile = true;
				document.PathAndFileName = file;

				fcd.Destroy ();
				return true;
			}

			fcd.Destroy ();
			return false;
		}

		private bool SaveFile (Document document, string file, FormatDescriptor format, Window parent)
		{
			bool fileIsNull = string.IsNullOrEmpty (file); // save initial state of file

			if (fileIsNull)
				file = document.PathAndFileName;

            if (string.IsNullOrEmpty (file))
                file = document.Filename; // fallback for CLI

			if (format == null)
				format = PintaCore.System.ImageFormats.GetFormatByFile (file);

			if (format == null || format.IsReadOnly ()) {
				MessageDialog md = new MessageDialog (parent, DialogFlags.Modal, MessageType.Error, ButtonsType.Ok, Catalog.GetString ("Pinta does not support saving images in this file format."), file);
				md.Title = Catalog.GetString ("Error");
                Pinta.Core.Document.MakeDialogNonInteractive(md);

				md.Run ();
				md.Destroy ();
				return false;
			}

			// Commit any pending changes
			PintaCore.Tools.Commit ();

			try {
				format.Exporter.Export (document, file, parent);
			} catch (GLib.GException e) { // Errors from GDK
				if (e.Message == "Image too large to be saved as ICO") {
					string primary = Catalog.GetString ("Image too large");
					string secondary = Catalog.GetString ("ICO files can not be larger than 255 x 255 pixels.");
					string message = string.Format (markup, primary, secondary);

					MessageDialog md = new MessageDialog (parent, DialogFlags.Modal, MessageType.Error,
					ButtonsType.Ok, message);
                    Pinta.Core.Document.MakeDialogNonInteractive(md);

					md.Run ();
					md.Destroy ();
					return false;
				} else if (e.Message.Contains ("Permission denied") && e.Message.Contains ("Failed to open")) {
					string primary = Catalog.GetString ("Failed to save image");
					// Translators: {0} is the name of a file that the user does not have write permission for.
					string secondary = string.Format(Catalog.GetString ("You do not have access to modify '{0}'. The file or folder may be read-only."), file);
					string message = string.Format (markup, primary, secondary);

					var md = new MessageDialog (parent, DialogFlags.Modal, MessageType.Error, ButtonsType.Ok, message);
                    Pinta.Core.Document.MakeDialogNonInteractive(md);

					md.Run ();
					md.Destroy ();

					return false;
				} else {
					throw e; // Only catch exceptions we know the reason for
				}
			} catch (OperationCanceledException) {
				return false;
			}

			// Set Pathname and Filename properties which triggers the document.Renamed event
			document.PathAndFileName = file;

			PintaCore.Tools.CurrentTool.DoAfterSave();

			// Mark the document as clean following the tool's after-save handler, which might
			// adjust history (e.g. undo changes that were committed before saving).
			document.Workspace.History.SetClean ();

			//Now the Document has been saved to the file it's associated with in this session.
			document.HasBeenSavedInSession = true;

			// fileIsNull is declare at the start of the method - if parameter 'file' was null
			// when calling this function, we can safely assume that no Gtk dialog was involved
			// (if there's a cleaner way to access the file parameter, let me know)
			if (fileIsNull /*&& File.Exists(file)*/)
			{
				// no need to remove item here, because no Gtk dialog was used
				string uri = new Uri (Path.GetFullPath (file)).AbsoluteUri;
				AddRecentFile (uri, GetMimeTypeForFile (file));
			}

			return true;
		}
		
		private string GetMimeTypeForFile (string file)
		{
			string ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();

			foreach (var pf in Gdk.Pixbuf.Formats)
			{
				if (!pf.IsWritable || pf.IsDisabled)
					continue;

				if (pf.Extensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)))
				{
					if (pf.MimeTypes != null && pf.MimeTypes.Length > 0)
						return pf.MimeTypes[0];
				}
			}

		return "application/octet-stream";
		}

		private void AddRecentFile (string uri, string mimeType)
		{
			var data = new RecentData
			{
				AppName = PintaCore.System.RecentData.AppName,
				AppExec = PintaCore.System.RecentData.AppExec,
				MimeType = mimeType
			};

			RecentManager.Default.AddFull(uri, data);
		}

		private bool ConfirmOverwrite (FileChooserDialog fcd, string file)
		{
			string primary = Catalog.GetString ("A file named \"{0}\" already exists. Do you want to replace it?");
			string secondary = Catalog.GetString ("The file already exists in \"{1}\". Replacing it will overwrite its contents.");
			string message = string.Format (markup, primary, secondary);

			MessageDialog md = new MessageDialog (fcd, DialogFlags.Modal | DialogFlags.DestroyWithParent,
				MessageType.Question, ButtonsType.None,
				true, message, System.IO.Path.GetFileName (file), fcd.CurrentFolder);

			md.AddButton (Stock.Cancel, ResponseType.Cancel);
			md.AddButton (Stock.Save, ResponseType.Ok);
			md.DefaultResponse = ResponseType.Cancel;
			md.AlternativeButtonOrder = new int[] { (int)ResponseType.Ok, (int)ResponseType.Cancel };
            Pinta.Core.Document.MakeDialogNonInteractive(md);

			int response = md.Run ();
			md.Destroy ();

			return response == (int)ResponseType.Ok;
		}

		private void OnFilterChanged (object o, GLib.NotifyArgs args)
		{
			FileChooserDialog fcd = (FileChooserDialog)o;

			// Ensure that the file filter is never blank.
			if (fcd.Filter == null)
			{
				fcd.Filter = PintaCore.System.ImageFormats.GetDefaultSaveFormat ().Filter;
				return;
			}

			// find the FormatDescriptor
			FormatDescriptor formatDesc = PintaCore.System.ImageFormats.Formats.Single (f => f.Filter == fcd.Filter);

			// adjust the filename
			var p = fcd.Filename;
			p = Path.ChangeExtension (Path.GetFileName (p), formatDesc.Extensions[0]);
			fcd.CurrentName = p;
		}
	}
}
