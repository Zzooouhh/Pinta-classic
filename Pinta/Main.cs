// 
// Main.cs
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
using Mono.Options;
using System.Collections.Generic;
using Pinta.Core;
using Mono.Unix;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Pinta
{
	class MainClass
	{
        private static TcpListener singleInstanceListener;
        private static int singleInstancePort = -1;
        private static bool no_document = false;
        private static int default_width = 800;
        private static int default_height = 600;
        
		[STAThread]
		public static void Main (string[] args)
		{
			string locale_dir = null;
			string force_lang = null;

			bool show_help = false;			
			bool show_version = false;
			int threads = -1;
			
			var p = new OptionSet () {
                                { "h|help", Catalog.GetString("Show this message and exit."), v => show_help = v != null },
                                { "default-width=", Catalog.GetString("New Pinta document will have default width equal to specified value."), (int v) => default_width = v },
                                { "default-height=", Catalog.GetString("New Pinta document will have default height equal to specified value."), (int v) => default_height = v },
                                { "locale-dir=", Catalog.GetString("Force Pinta to use the specified locale directory (provide full path to directory)."), (string v) => locale_dir = v},
                                { "l|language=", Catalog.GetString("Force Pinta to use the specified LANGUAGE env variable (use format like \"de\")."), (string v) => force_lang = v},
                                { "n|no-document", Catalog.GetString("Launch Pinta without a new document open (ignored if a target was specified)."), v => no_document = v != null },
                                { "p|port=", Catalog.GetString("Launch Pinta as a single instance TCP server with the specified port number (or use an existing Pinta instance with the corresponding port number)."), (int v) => singleInstancePort = v },
								{ "r|render-threads=", Catalog.GetString ("Specify number of threads to use for rendering"), (int v) => threads = v },
                                { "v|version", Catalog.GetString("Display the application version."), v => show_version = v != null }
			};

			if (string.IsNullOrEmpty(locale_dir))
				locale_dir = Path.Combine (SystemManager.GetDataRootDirectory (), "locale");
			
			List<string> extra;
			try {
				extra = p.Parse (args);
			} catch (OptionException e) {
				Console.WriteLine (e.Message);
                                ShowHelp (p);
				return;
			}

			if (!string.IsNullOrEmpty(force_lang)) {

				Environment.SetEnvironmentVariable("LANGUAGE", force_lang);

				string mo = Path.Combine(
					locale_dir,
					force_lang,
					"LC_MESSAGES",
					"pinta.mo"
				);

				if (File.Exists(mo))
					Console.WriteLine ($"Using language override: {force_lang}");
				else
					Console.WriteLine ($"Language file not found! ({force_lang})");
			}

			try {
				Catalog.Init ("pinta", locale_dir);
			} catch (Exception ex) {
				Console.WriteLine (ex);
			}
			
            if (show_version)
            {
                Console.WriteLine (PintaCore.ApplicationVersion);
                return;
            }

            if (show_help)
            {
                ShowHelp (p);
                return;
            }

            if (singleInstancePort != -1) {

                try
                {
                    singleInstanceListener = new TcpListener(IPAddress.Loopback, singleInstancePort);
                    singleInstanceListener.Start();
                    singleInstancePort = -1;
                }
                catch (SocketException)
                {
                    SendToExistingInstance(args);
                    return;
                }

                if (singleInstancePort != -1) {
                }
                StartTcpServerLoop();
            } else {
                Console.Error.WriteLine(
                    $"Pinta running in new-instance mode."
                );
            }
            
			GLib.ExceptionManager.UnhandledException += new GLib.UnhandledExceptionHandler (ExceptionManager_UnhandledException);

			if (SystemManager.GetOperatingSystem () == OS.Windows) {
				SetWindowsGtkPath ();
			}
			
			Application.Init ();
			new MainWindow ();
			
			if (threads != -1)
				Pinta.Core.PintaCore.System.RenderThreads = threads;

			if (SystemManager.GetOperatingSystem () == OS.Mac) {
				RegisterForAppleEvents ();
			}

            OpenFilesFromCommandLine(extra);
			
			Application.Run ();
		}

        private static void ShowHelp (OptionSet p)
        {
            Console.WriteLine (Catalog.GetString ("Usage: pinta [files]"));
            Console.WriteLine ();
            Console.WriteLine (Catalog.GetString ("Options: "));
            p.WriteOptionDescriptions (Console.Out);
        }

		private static void OpenFilesFromCommandLine (List<string> extra)
		{
			// Ignore the process serial number parameter on Mac OS X
			if (PintaCore.System.OperatingSystem == OS.Mac && extra.Count > 0)
			{
				if (extra[0].StartsWith ("-psn_"))
				{
					extra.RemoveAt (0);
				}
			}

            if (extra.Count > 0)
            {
                foreach (var file in extra)
                {
                    PintaCore.Workspace.OpenFile (file);
                    
                    string fullPath = System.IO.Path.GetFullPath (file);
                    
                    string folder = System.IO.Path.GetDirectoryName (fullPath);

                    // This makes GetDialogDirectory() return this folder instead of Pictures
                    if (System.IO.Directory.Exists (folder)) {
                        PintaCore.System.LastDialogDirectory = folder;
                        
                        // Optional: Try to update Settings too if you can access the key string.
                        // If LastDialogDirSettingKey is public, you can do:
                        // PintaCore.Settings.PutSetting (LastDialogDirSettingKey, folder);
                    }
                }
            }
			else
			{
				// Create a blank document
				if (!no_document) {
					// PintaCore.System.LastDialogDirectory = PintaCore.System.DefaultDialogDirectory; // uncomment to make new documents use default directory at first
					PintaCore.Workspace.NewDocument (new Gdk.Size (default_width, default_height), new Cairo.Color (1, 1, 1));
				}
			}
		}

        private static void StartTcpServerLoop()
        {
            Thread thread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        var client = singleInstanceListener.AcceptTcpClient();

                        List<string> filesToOpen = new List<string>();
                        using (var stream = client.GetStream())
                        using (var reader = new StreamReader(stream))
                        {
                            string file;
                            while ((file = reader.ReadLine()) != null)
                            {
                                if (!string.IsNullOrWhiteSpace(file))
                                    filesToOpen.Add(file);
                            }
                        }

                        client.Close();

                        if (filesToOpen.Count > 0)
                        {
                            // Queue the whole batch at once
                            Application.Invoke(delegate
                            {
                                OpenFilesInWorkspace(filesToOpen);
                            });
                        } else {
                            Console.Error.WriteLine(
                                $"Error: no file found to open"
                            );
                        }
                    }
                    catch
                    {
                        break;
                    }
                }
            });

            thread.IsBackground = true;
            thread.Start();
        }

        // Open a batch of files safely
        private static void OpenFilesInWorkspace(List<string> files)
        {
            foreach (var file in files)
            {
                if (File.Exists(file))
                {
                    PintaCore.Chrome.MainWindow.Present();
                    try
                    {
                        PintaCore.Workspace.OpenFile (file);
                    }
                    catch (FormatException)
                    {
                        // Only triggered for truly unsupported files
                        Console.Error.WriteLine(
                            $"Error: could not open file {file}: unsupported format"
                        );
                    }
                } else {
                    Console.Error.WriteLine(
                        $"Error: could not open file {file}: not found"
                    );
                }
            }
        }

        private static bool SendToExistingInstance(string[] args)
        {
            try
            {
                var client = new TcpClient();
                client.Connect(IPAddress.Loopback, singleInstancePort);

                using (var stream = client.GetStream())
                using (var writer = new StreamWriter(stream))
                {
                    foreach (var arg in args)
                    {
                        if (!arg.StartsWith("-"))
                            writer.WriteLine(Path.GetFullPath(arg));
                    }
                }

                client.Close();
                return true;
            }
            catch
            {        
                Console.Error.WriteLine(
                    $"Warning: Could not bind to TCP port {singleInstancePort}. " +
                    "Single-instance file forwarding may not work."
                );
                return false;
            }
        }

		private static void ExceptionManager_UnhandledException (GLib.UnhandledExceptionArgs args)
		{
			Exception ex = (Exception)args.ExceptionObject;
			PintaCore.Chrome.ShowErrorDialog (PintaCore.Chrome.MainWindow,
			                                  string.Format ("{0}:\n{1}", "Unhandled exception", ex.Message),
			                                  ex.ToString ());
		}

		/// <summary>
		/// Registers for OSX-specific events, like quitting from the dock.
		/// </summary>
		static void RegisterForAppleEvents ()
		{
			MacInterop.ApplicationEvents.Quit += (sender, e) => {
				GLib.Timeout.Add (10, delegate {
					PintaCore.Actions.File.Exit.Activate ();
					return false;
				});
				e.Handled = true;
			};

			MacInterop.ApplicationEvents.Reopen += (sender, e) => {
				var window = PintaCore.Chrome.MainWindow;
				window.Deiconify ();
				window.Hide ();
				window.Show ();
				window.Present ();
				e.Handled = true;
			};

			MacInterop.ApplicationEvents.OpenDocuments += (sender, e) => {
				if (e.Documents != null) {
					GLib.Timeout.Add (10, delegate {
						foreach (string filename in e.Documents.Keys) {
							System.Console.Error.WriteLine ("Opening: {0}", filename);
							PintaCore.Workspace.OpenFile (filename);
						}
						return false;
					});
				}
				e.Handled = true;
			};
		}

		[DllImport ("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs (UnmanagedType.Bool)]
		static extern bool SetDllDirectory (string lpPathName);

		/// <summary>
		/// Explicitly add GTK+ to the search path.
		/// From MonoDevelop: https://bugzilla.xamarin.com/show_bug.cgi?id=10558
		/// </summary>
		private static void SetWindowsGtkPath ()
		{
			string location = null;
			using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey (@"SOFTWARE\Xamarin\GtkSharp\InstallFolder")) {
				if (key != null) {
					location = key.GetValue (null) as string;
				}
			}

			if (location == null || !File.Exists (Path.Combine (location, "bin", "libgtk-win32-2.0-0.dll"))) {
				System.Console.Error.WriteLine ("Did not find registered GTK# installation");
				return;
			}

			var path = Path.Combine (location, @"bin");
			try {
				if (SetDllDirectory (path)) {
					return;
				}
			}
			catch (EntryPointNotFoundException) {
			}

			System.Console.Error.WriteLine ("Unable to set GTK# dll directory");
		}
	}
}
