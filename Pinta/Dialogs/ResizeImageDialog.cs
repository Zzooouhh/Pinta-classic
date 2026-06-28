// 
// ResizeImageDialog.cs
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
using Gtk;
using Mono.Unix;
using Pinta.Core;

namespace Pinta
{
	public class ResizeImageDialog : Dialog
	{
        private ComboBox resample_combo;
		private RadioButton percentageRadio;
		private RadioButton absoluteRadio;
		private SpinButton percentageSpinner;
		private SpinButton widthSpinner;
		private SpinButton heightSpinner;
		private CheckButton aspectCheckbox;

		private bool value_changing;
        private const string ResamplingSetting = "ResamplingType";
		
		public ResizeImageDialog () : base (Catalog.GetString ("Resize Image"), PintaCore.Chrome.MainWindow,
		                                    DialogFlags.Modal,
											Gtk.Stock.Cancel, Gtk.ResponseType.Cancel,
											Gtk.Stock.Ok, Gtk.ResponseType.Ok)
		{
			Build ();

			aspectCheckbox.Active = true;

			widthSpinner.Value = PintaCore.Workspace.ImageSize.Width;
			heightSpinner.Value = PintaCore.Workspace.ImageSize.Height;

			percentageRadio.Toggled += new EventHandler (percentageRadio_Toggled);
			absoluteRadio.Toggled += new EventHandler (absoluteRadio_Toggled);
			percentageRadio.Toggle ();

			percentageSpinner.Value = 100;
			percentageSpinner.ValueChanged += new EventHandler (percentageSpinner_ValueChanged);

			widthSpinner.ValueChanged += new EventHandler (widthSpinner_ValueChanged);
			heightSpinner.ValueChanged += new EventHandler (heightSpinner_ValueChanged);
			
			AlternativeButtonOrder = new int[] { (int) Gtk.ResponseType.Ok, (int) Gtk.ResponseType.Cancel };
			DefaultResponse = Gtk.ResponseType.Ok;

			widthSpinner.ActivatesDefault = true;
			heightSpinner.ActivatesDefault = true;
			percentageSpinner.ActivatesDefault = true;
			percentageSpinner.GrabFocus();
		}

		#region Public Methods
		public void SaveChanges ()
		{
			PintaCore.Workspace.ResizeImage (widthSpinner.ValueAsInt, heightSpinner.ValueAsInt, this.ResamplingFilter);
		}
		#endregion
		
		#region Private Methods
		private void heightSpinner_ValueChanged (object sender, EventArgs e)
		{
			if (value_changing)
				return;
				
			if (aspectCheckbox.Active) {
				value_changing = true;
				widthSpinner.Value = (int)((heightSpinner.Value * PintaCore.Workspace.ImageSize.Width) / PintaCore.Workspace.ImageSize.Height);
				value_changing = false;
			}
		}

		private void widthSpinner_ValueChanged (object sender, EventArgs e)
		{
			if (value_changing)
				return;

			if (aspectCheckbox.Active) {
				value_changing = true;
				heightSpinner.Value = (int)((widthSpinner.Value * PintaCore.Workspace.ImageSize.Height) / PintaCore.Workspace.ImageSize.Width);
				value_changing = false;
			}
		}

		private void percentageSpinner_ValueChanged (object sender, EventArgs e)
		{
			widthSpinner.Value = (int)(PintaCore.Workspace.ImageSize.Width * (percentageSpinner.ValueAsInt / 100f));
			heightSpinner.Value = (int)(PintaCore.Workspace.ImageSize.Height * (percentageSpinner.ValueAsInt / 100f));
		}

		private void absoluteRadio_Toggled (object sender, EventArgs e)
		{
			RadioToggle ();
		}

		private void percentageRadio_Toggled (object sender, EventArgs e)
		{
			RadioToggle ();
		}

		private void RadioToggle ()
		{
			if (percentageRadio.Active) {
				percentageSpinner.Sensitive = true;

				widthSpinner.Sensitive = false;
				heightSpinner.Sensitive = false;
				aspectCheckbox.Sensitive = false;
			} else {
				percentageSpinner.Sensitive = false;

				widthSpinner.Sensitive = true;
				heightSpinner.Sensitive = true;
				aspectCheckbox.Sensitive = true;
			}
		}

		private void Build()
		{
			Icon = PintaCore.Resources.GetIcon ("Menu.Image.Resize.png");
			WindowPosition = WindowPosition.CenterOnParent;

			DefaultWidth = 300;
			DefaultHeight = 200;

			percentageRadio = new RadioButton (Catalog.GetString ("By percentage:"));
			absoluteRadio = new RadioButton (percentageRadio, Catalog.GetString ("By absolute size:"));

			percentageSpinner = new SpinButton (1, 1000, 1);
			widthSpinner = new SpinButton (1, 10000, 1);
			heightSpinner = new SpinButton (1, 10000, 1);

			aspectCheckbox = new CheckButton (Catalog.GetString ("Maintain aspect ratio"));

			const int spacing = 6;
			var main_vbox = new VBox () { Spacing = spacing, BorderWidth = 12 };

            Label resample_label = new Label (Catalog.GetString ("Resampling:"));
            resample_label.Xalign = 0;
            resample_label.Xpad = 5;

            resample_combo = new ComboBox ();

            // Create a ListStore to hold the text (Display) and the ID (for logic)
            var resample_store = new ListStore (typeof (string), typeof (string));

            // Add Options (Format: DisplayText, InternalID)
            resample_store.AppendValues (Catalog.GetString ("Bicubic (Best)"), "Best");
            resample_store.AppendValues (Catalog.GetString ("Bilinear (Good)"), "Good");
            resample_store.AppendValues (Catalog.GetString ("Nearest Neighbor (Fast)"), "Fast");

            resample_combo.Model = resample_store;

            CellRendererText cell = new CellRendererText ();
            resample_combo.PackStart (cell, false);
            resample_combo.AddAttribute (cell, "text", 0);

            resample_combo.Active = 0; // Set Default to "Bicubic" (index 0)

            var hbox_resample = new HBox () { Spacing = spacing };
            hbox_resample.PackStart (resample_label, true, true, 0); // True = Expand
            hbox_resample.PackStart (resample_combo, false, false, 0);

            main_vbox.PackStart (hbox_resample, false, false, 0);

            // Get the saved ID string (Default to "Best" for Bicubic)
            string savedId = PintaCore.Settings.GetSetting (ResamplingSetting, "Best");

            // Iterate through the ComboBox model to find the matching ID
            TreeIter iter;
            if (resample_combo.Model.GetIterFirst (out iter)) {
                do {
                    string id = (string)resample_combo.Model.GetValue (iter, 1);
                    if (id == savedId) {
                        resample_combo.SetActiveIter (iter);
                        break;
                    }
                } while (resample_combo.Model.IterNext (ref iter));
            }

			var hbox_percent = new HBox () { Spacing = spacing };
			hbox_percent.PackStart (percentageRadio, true, true, 0);
			hbox_percent.PackStart (percentageSpinner, false, false, 0);
			hbox_percent.PackEnd (new Label ("%"), false, false, 0);
			main_vbox.PackStart (hbox_percent, false, false, 0);

			main_vbox.PackStart (absoluteRadio, false, false, 0);

			var hbox_width = new HBox () { Spacing = spacing };
			hbox_width.PackStart (new Label (Catalog.GetString ("Width:")), false, false, 0);
			hbox_width.PackStart (widthSpinner, false, false, 0);
			hbox_width.PackStart (new Label (Catalog.GetString ("pixels")), false, false, 0);
			main_vbox.PackStart (hbox_width, false, false, 0);

			var hbox_height = new HBox () { Spacing = spacing };
			hbox_height.PackStart (new Label (Catalog.GetString ("Height:")), false, false, 0);
			hbox_height.PackStart (heightSpinner, false, false, 0);
			hbox_height.PackStart (new Label (Catalog.GetString ("pixels")), false, false, 0);
			main_vbox.PackStart (hbox_height, false, false, 0);

			main_vbox.PackStart (aspectCheckbox, false, false, 0);

			VBox.BorderWidth = 2;
			VBox.Add (main_vbox);

			ShowAll ();
		}
        
        protected override void OnResponse (ResponseType type)
        {
            base.OnResponse (type); // Ensure default close logic runs

            if (type == ResponseType.Ok) {
                // Get the active item's ID
                Gtk.TreeIter iter;
                if (resample_combo.GetActiveIter (out iter)) {
                    string id = (string)resample_combo.Model.GetValue (iter, 1);

                    // Save it to Settings
                    PintaCore.Settings.PutSetting (ResamplingSetting, id);
                }
            }
        }
        
        public Cairo.Filter ResamplingFilter {
            get {
                if (resample_combo == null || resample_combo.Active < 0)
                    return Cairo.Filter.Best; // Fallback to Bicubic

                Gtk.TreeIter iter;
                if (resample_combo.GetActiveIter (out iter)) {
                    string id = (string)resample_combo.Model.GetValue (iter, 1);

                    switch (id) {
                        case "Fast": // Nearest Neighbor
                            return Cairo.Filter.Fast;
                        case "Good": // Bilinear
                            return Cairo.Filter.Good;
                        case "Best": // Bicubic
                        default:
                            return Cairo.Filter.Best;
                    }
                }
                return Cairo.Filter.Best; // Fallback
            }
        }
		#endregion
	}
}

