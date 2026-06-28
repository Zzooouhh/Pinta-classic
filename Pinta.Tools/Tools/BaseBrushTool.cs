// 
// BaseBrushTool.cs
//  
// Author:
//       Joseph Hillenbrand <joehillen@gmail.com>
// 
// Copyright (c) 2010 Joseph Hillenbrand
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
using Cairo;
using Gtk;
using Pinta.Core;
using Mono.Unix;

namespace Pinta.Tools
{
	// This is a base class for brush type tools (paintbrush, eraser, etc)
	public class BaseBrushTool : BaseTool
	{
		protected ToolBarComboBox brush_width;
		protected ToolBarLabel brush_width_label;
		protected ToolBarButton brush_width_minus;
		protected ToolBarButton brush_width_plus;
		
		protected ImageSurface undo_surface;
		protected bool surface_modified;

        // CHANGE private TO protected for these three:
        protected PointD start_point_drawing;
        protected Gdk.Rectangle last_invalidated_rect;
        protected bool was_shift_held = false;

        // These stay private/static as they don't need to be accessed directly by derived tools
        protected static bool is_shift_down = false;
        protected static bool is_ctrl_down = false;
        private static bool handlers_attached = false;

		protected override bool ShowAntialiasingButton { get { return true; } }
	    
		public virtual int BrushWidth { 
			get { 
				int width;
				if (brush_width != null)
				{
					if (Int32.TryParse(brush_width.ComboBox.ActiveText, out width))
					{
						if (width > 0)
						{
							(brush_width.ComboBox as Gtk.ComboBoxEntry).Entry.Text = width.ToString();
							return width;
						}
					}

					(brush_width.ComboBox as Gtk.ComboBoxEntry).Entry.Text = DEFAULT_BRUSH_WIDTH.ToString();
				}
				return DEFAULT_BRUSH_WIDTH;
			}
			set { (brush_width.ComboBox as Gtk.ComboBoxEntry).Entry.Text = value.ToString (); }
		}
		
		#region ToolBar
		protected override void OnBuildToolBar (Toolbar tb)
		{
			base.OnBuildToolBar (tb);
			
			if (brush_width_label == null)
				brush_width_label = new ToolBarLabel (string.Format (" {0}: ", Catalog.GetString ("Brush width")));
			
			tb.AppendItem (brush_width_label);
	
			if (brush_width_minus == null) {
				brush_width_minus = new ToolBarButton ("Toolbar.MinusButton.png", "", Catalog.GetString ("Decrease brush size"));
				brush_width_minus.Clicked += MinusButtonClickedEvent;
			}
			
			tb.AppendItem (brush_width_minus);
		
			if (brush_width == null)
				brush_width = new ToolBarComboBox (65, 1, true, "1", "2", "3", "4", "5", "6", "7", "8", "9",
				"10", "11", "12", "13", "14", "15", "20", "25", "30", "35",
				"40", "45", "50", "55");
			
			tb.AppendItem (brush_width);
			
			if (brush_width_plus == null) {
				brush_width_plus = new ToolBarButton ("Toolbar.PlusButton.png", "", Catalog.GetString ("Increase brush size"));
				brush_width_plus.Clicked += PlusButtonClickedEvent;
			}
			
			tb.AppendItem (brush_width_plus);
		}
		
		protected virtual void MinusButtonClickedEvent (object o, EventArgs args)
		{
            bool shiftHeld = false;
            Gdk.Event ev = Gtk.Global.CurrentEvent;
            if (ev != null && ev.Type == Gdk.EventType.ButtonRelease) {
                Gdk.EventButton btn = (Gdk.EventButton)ev;
                if ((btn.State & Gdk.ModifierType.ShiftMask) != 0) {
                    shiftHeld = true;
                }
            }

            int change = shiftHeld ? 5 : 1;
            int newVal = BrushWidth - change;
            if (newVal < 1) newVal = 1;
            
            BrushWidth = newVal;
        }
		
		protected virtual void PlusButtonClickedEvent (object o, EventArgs args)
		{
            bool shiftHeld = false;
            Gdk.Event ev = Gtk.Global.CurrentEvent;
            if (ev != null && ev.Type == Gdk.EventType.ButtonRelease) {
                Gdk.EventButton btn = (Gdk.EventButton)ev;
                if ((btn.State & Gdk.ModifierType.ShiftMask) != 0) {
                    shiftHeld = true;
                }
            }

            int change = shiftHeld ? 5 : 1;
            int newVal = BrushWidth + change;
            
            BrushWidth = newVal;
        }
		#endregion

		#region Key Handlers
        
        protected override void OnKeyDown(Gtk.DrawingArea canvas, Gtk.KeyPressEventArgs args)
        {
			Gdk.Key keyPressed = args.Event.Key;

            if (keyPressed == Gdk.Key.A && (args.Event.State & Gdk.ModifierType.ControlMask) != 0) // ctrl + shift + A = toggle antialiasing
            {
                var tool = PintaCore.Tools.CurrentTool;
                if (tool != null)
                    tool.UseAntialiasing = !tool.UseAntialiasing;
                args.RetVal = true;
            }
            else if (keyPressed == Gdk.Key.bracketleft || keyPressed == Gdk.Key.braceleft)
            {
                if ((args.Event.State & Gdk.ModifierType.ShiftMask) == Gdk.ModifierType.ShiftMask)
                    if (BrushWidth > 5)
                        BrushWidth -= 5;
                    else
                        BrushWidth = 1;
                else if (BrushWidth > 1)
                        BrushWidth--;
                args.RetVal = true;
            }
            else if (keyPressed == Gdk.Key.bracketright || keyPressed == Gdk.Key.braceright)
            {
                if ((args.Event.State & Gdk.ModifierType.ShiftMask) == Gdk.ModifierType.ShiftMask)
                    BrushWidth += 5;
                else
                    BrushWidth++;
                args.RetVal = true;
            }
            else
            {
                base.OnKeyDown(canvas, args);
            }
        }

        protected override void OnKeyUp(Gtk.DrawingArea canvas, Gtk.KeyReleaseEventArgs args)
        {
			Gdk.Key keyReleased = args.Event.Key;
            if (keyReleased == Gdk.Key.bracketleft || keyReleased == Gdk.Key.bracketright)
            {
                args.RetVal = true;
            }
            else
            {
                base.OnKeyUp(canvas, args);
            }
        }
		#endregion

		#region Mouse Handlers
		protected override void OnMouseUp (Gtk.DrawingArea canvas, Gtk.ButtonReleaseEventArgs args, Cairo.PointD point)
		{
			Document doc = PintaCore.Workspace.ActiveDocument;

			if (undo_surface != null) {
				if (surface_modified)
					doc.History.PushNewItem (new SimpleHistoryItem (Icon, Name, undo_surface, doc.CurrentUserLayerIndex));
				else if (undo_surface != null)
					(undo_surface as IDisposable).Dispose ();
			}
			
			surface_modified = false;
			undo_surface = null;
			mouse_button = 0;
            
            // Cleanup
            last_invalidated_rect = Gdk.Rectangle.Zero;
            was_shift_held = false;
		}
		
		protected override void OnMouseDown (Gtk.DrawingArea canvas, Gtk.ButtonPressEventArgs args, Cairo.PointD point)
		{
			// If we are already drawing, ignore any additional mouse down events
			if (mouse_button > 0)
				return;

			Document doc = PintaCore.Workspace.ActiveDocument;

			surface_modified = false;
			undo_surface = doc.CurrentUserLayer.Surface.Clone ();
			mouse_button = args.Event.Button;
			
            start_point_drawing = point;
            last_invalidated_rect = Gdk.Rectangle.Zero;
            was_shift_held = is_shift_down; // Sync with current static state
            
			OnMouseMove (canvas, null, point);
		}
		#endregion
		
		#region Protected Methods
		protected Gdk.Rectangle GetRectangleFromPoints (Point a, Point b)
		{
			int x = Math.Min (a.X, b.X) - BrushWidth - 2;
			int y = Math.Min (a.Y, b.Y) - BrushWidth - 2;
			int w = Math.Max (a.X, b.X) - x + (BrushWidth * 2) + 4;
			int h = Math.Max (a.Y, b.Y) - y + (BrushWidth * 2) + 4;
			
			return new Gdk.Rectangle (x, y, w, h);
		}

		protected Gdk.Rectangle GetRectangleFromPoints (Gdk.Point a, Gdk.Point b)
		{
			int x = Math.Min (a.X, b.X);
			int y = Math.Min (a.Y, b.Y);
			int w = Math.Max (a.X, b.X);
			int h = Math.Max (a.Y, b.Y);
			
			var rect = new Gdk.Rectangle (x, y, w, h);
			rect.Inflate (BrushWidth, BrushWidth);
			return rect;
		}
		#endregion
	}
}
