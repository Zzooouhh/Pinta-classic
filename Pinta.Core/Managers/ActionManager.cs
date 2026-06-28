// 
// ActionManager.cs
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
using System.Collections.Generic;
using ClipperLibrary;

namespace Pinta.Core
{
	public class ActionManager
	{
		public AccelGroup AccelGroup { get; private set; }
		
		public FileActions File { get; private set; }
		public EditActions Edit { get; private set; }
		public ViewActions View { get; private set; }
		public ImageActions Image { get; private set; }
		public LayerActions Layers { get; private set; }
		public AdjustmentsActions Adjustments { get; private set; }
		public EffectsActions Effects { get; private set; }
		public AddinActions Addins { get; private set; }
		public WindowActions Window { get; private set; }
		public HelpActions Help { get; private set; }
		
		public ActionManager ()
		{
			AccelGroup = new AccelGroup ();
			
			File = new FileActions ();
			Edit = new EditActions ();
			View = new ViewActions ();
			Image = new ImageActions ();
			Layers = new LayerActions ();
			Adjustments = new AdjustmentsActions ();
			Effects = new EffectsActions ();
			Addins = new AddinActions ();
			Window = new WindowActions ();
			Help = new HelpActions ();
		}
		
		public void CreateMainMenu (Gtk.MenuBar menu)
		{
			// File menu
			ImageMenuItem file = (ImageMenuItem)menu.Children[0];
			file.Submenu = new Menu ();
			File.CreateMainMenu ((Menu)file.Submenu);
			
			//Edit menu
			ImageMenuItem edit = (ImageMenuItem)menu.Children[1];
			edit.Submenu = new Menu ();
			Edit.CreateMainMenu ((Menu)edit.Submenu);
			
			// View menu
			ImageMenuItem view = (ImageMenuItem)menu.Children[2];
			View.CreateMainMenu ((Menu)view.Submenu);
			
			// Image menu
			ImageMenuItem image = (ImageMenuItem)menu.Children[3];
			image.Submenu = new Menu ();
			Image.CreateMainMenu ((Menu)image.Submenu);
			
			//Layers menu
			ImageMenuItem layer = (ImageMenuItem)menu.Children[4];
			layer.Submenu = new Menu ();
			Layers.CreateMainMenu ((Menu)layer.Submenu);
			
			//Adjustments menu
			ImageMenuItem adj = (ImageMenuItem)menu.Children[5];
			adj.Submenu = new Menu ();
			Adjustments.CreateMainMenu ((Menu)adj.Submenu);

			// Effects menu
			ImageMenuItem eff = (ImageMenuItem)menu.Children[6];
			eff.Submenu = new Menu ();
			Effects.CreateMainMenu ((Menu)eff.Submenu);

			// Add-ins menu
			ImageMenuItem addins = (ImageMenuItem)menu.Children[7];
			addins.Submenu = new Menu ();
			Addins.CreateMainMenu ((Menu)addins.Submenu);

			// Window menu
			ImageMenuItem window = (ImageMenuItem)menu.Children[8];
			window.Submenu = new Menu ();
			Window.CreateMainMenu ((Menu)window.Submenu);
			
			//Help menu
			ImageMenuItem help = (ImageMenuItem)menu.Children[9];
			help.Submenu = new Menu ();
			Help.CreateMainMenu ((Menu)help.Submenu);
		}
		
		public void CreateToolBar (Gtk.Toolbar toolbar)
		{
			toolbar.AppendItem (File.New.CreateToolBarItem ());
			toolbar.AppendItem (File.Open.CreateToolBarItem ());
			toolbar.AppendItem (File.Save.CreateToolBarItem ());
			// Printing is disabled for now until it is fully functional.
#if false
			toolbar.AppendItem (File.Print.CreateToolBarItem ());
#endif
			toolbar.AppendItem (new SeparatorToolItem ());

			// Cut/Copy/Paste comes before Undo/Redo on Windows
			if (PintaCore.System.OperatingSystem == OS.Windows) {
				toolbar.AppendItem (Edit.Cut.CreateToolBarItem ());
				toolbar.AppendItem (Edit.Copy.CreateToolBarItem ());
				toolbar.AppendItem (Edit.Paste.CreateToolBarItem ());
				toolbar.AppendItem (new SeparatorToolItem ());
				toolbar.AppendItem (Edit.Undo.CreateToolBarItem ());
				toolbar.AppendItem (Edit.Redo.CreateToolBarItem ());
			} else {
				toolbar.AppendItem (Edit.Undo.CreateToolBarItem ());
				toolbar.AppendItem (Edit.Redo.CreateToolBarItem ());
				toolbar.AppendItem (new SeparatorToolItem ());
				toolbar.AppendItem (Edit.Cut.CreateToolBarItem ());
				toolbar.AppendItem (Edit.Copy.CreateToolBarItem ());
				toolbar.AppendItem (Edit.Paste.CreateToolBarItem ());
			}

			toolbar.AppendItem (new SeparatorToolItem ());
			toolbar.AppendItem (Image.CropToSelection.CreateToolBarItem ());
			toolbar.AppendItem (Edit.Deselect.CreateToolBarItem ());
			View.CreateToolBar (toolbar);
            CreateToolBarStatus(toolbar);
        }
        
        private void CreateToolBarStatus (Gtk.Toolbar toolbar)
        {
			toolbar.AppendItem (new SeparatorToolItem ());
			toolbar.AppendItem (new ToolBarImage ("StatusBar.CursorXY.png"));

			ToolBarLabel cursor = new ToolBarLabel ("  0, 0");

			toolbar.AppendItem (cursor);

            Cairo.Color currentColor = new Cairo.Color(0, 0, 0);
            Gtk.DrawingArea colorSwatch = new Gtk.DrawingArea();
            ToolBarLabel hexLabel = new ToolBarLabel ("");

			PintaCore.Chrome.LastCanvasCursorPointChanged += delegate {

                if (!PintaCore.Workspace.HasOpenDocuments)
                {
                    cursor.Text = "  0, 0";
                    hexLabel.Text = "";
                    return;
                }

                Gdk.Point pt = PintaCore.Chrome.LastCanvasCursorPoint;
                cursor.Text = string.Format ("  {0}, {1}", pt.X, pt.Y);

                var doc = PintaCore.Workspace.ActiveDocument;

                if (!doc.Workspace.PointInCanvas (new Cairo.PointD(pt.X, pt.Y)))
                {
                    hexLabel.Text = "";
                    return;
                }

                var surface = doc.CurrentUserLayer.Surface;

                if (pt.X < 0 || pt.Y < 0 || pt.X >= surface.Width || pt.Y >= surface.Height)
                {
                    hexLabel.Text = "";
                    return;
                }

                unsafe {
                    surface.Flush();

                    byte* dataPtr = (byte*)surface.DataPtr;
                    int stride = surface.Stride;
                    byte* pixel = dataPtr + pt.Y * stride + pt.X * 4;

                    byte b = pixel[0];
                    byte g = pixel[1];
                    byte r = pixel[2];
                    byte a = pixel[3];

                    // Update hex text
                    hexLabel.Text = string.Format("  #{0:X2}{1:X2}{2:X2}({3:X2})", r, g, b, a);

                    // Update swatch color
                    currentColor = new Cairo.Color(r / 255.0, g / 255.0, b / 255.0);

                    colorSwatch.QueueDraw();

                }
			};


			toolbar.AppendItem(new SeparatorToolItem());
			toolbar.AppendItem(new ToolBarImage("Tools.RectangleSelect.png"));

			ToolBarLabel SelectionSize = new ToolBarLabel("  0, 0");

			toolbar.AppendItem(SelectionSize);

			PintaCore.Workspace.SelectionChanged += delegate
			{
			    if (!PintaCore.Workspace.HasOpenDocuments)
			    {
			        SelectionSize.Text = "  0, 0";
                    return;
                }

                double minX = double.MaxValue;
				double minY = double.MaxValue;
				double maxX = double.MinValue;
				double maxY = double.MinValue;

				// Calculate the minimum rectangular bounds that surround the current selection.
				foreach (List<IntPoint> li in PintaCore.Workspace.ActiveDocument.Selection.SelectionPolygons)
				{
					foreach (IntPoint ip in li)
					{
						if (minX > ip.X)
						{
							minX = ip.X;
						}

						if (minY > ip.Y)
						{
							minY = ip.Y;
						}

						if (maxX < ip.X)
						{
							maxX = ip.X;
						}

						if (maxY < ip.Y)
						{
							maxY = ip.Y;
						}
					}
				}

				double xDiff = maxX - minX;
				double yDiff = maxY - minY;

				if (double.IsNegativeInfinity(xDiff))
				{
					xDiff = 0d;
				}

				if (double.IsNegativeInfinity(yDiff))
				{
					yDiff = 0d;
				}

				SelectionSize.Text = string.Format("  {0}, {1}   ({2}, {3})", xDiff, yDiff, minX, minY);
			};
                
            toolbar.AppendItem(new SeparatorToolItem());

            // Small color preview box
            colorSwatch.SetSizeRequest(16, 16);
            Gtk.Alignment align = new Gtk.Alignment(0.5f, 0.5f, 0, 0);
            align.Add(colorSwatch);

            ToolItem swatchItem = new ToolItem();
            swatchItem.Add(colorSwatch);
            swatchItem.Add(align);
            swatchItem.Expand = false;
            toolbar.Insert(swatchItem, -1);

            // Hex label
            toolbar.AppendItem(hexLabel);

            colorSwatch.ExposeEvent += (o, args) =>
            {
                using (var g = Gdk.CairoHelper.Create(colorSwatch.GdkWindow))
                {
                    g.SetSourceRGB(currentColor.R, currentColor.G, currentColor.B);
                    g.Rectangle(0, 0, colorSwatch.Allocation.Width, colorSwatch.Allocation.Height);
                    g.Fill();

                    // Optional border
                    g.SetSourceRGB(0, 0, 0);
                    g.Rectangle(0.5, 0.5, colorSwatch.Allocation.Width - 1, colorSwatch.Allocation.Height - 1);
                    g.Stroke();
                }
            };
		}
		
		public void RegisterHandlers ()
		{
			File.RegisterHandlers ();
			Edit.RegisterHandlers ();
			Image.RegisterHandlers ();
			Layers.RegisterHandlers ();
			View.RegisterHandlers ();
			Help.RegisterHandlers ();
		}
	}
}
