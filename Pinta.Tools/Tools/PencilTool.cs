// 
// PencilTool.cs
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
using Cairo;
using Gtk;
using Pinta.Core;
using Mono.Unix;

namespace Pinta.Tools
{
	public class PencilTool : BaseTool
	{		
		private Point last_point = point_empty;
		
		private ImageSurface undo_surface;
		private bool surface_modified;
        private PointD start_point_drawing;
        private Gdk.Rectangle last_invalidated_rect; // Used to fix the visual trail
        private bool was_shift_held = false; // Track the state in the previous frame

		protected override bool ShowAlphaBlendingButton { get { return true; } }

		public PencilTool ()
		{
		}

		#region Properties
		public override string Name { get { return Catalog.GetString ("Pencil"); } }
		public override string Icon { get { return "Tools.Pencil.png"; } }
		public override string StatusBarText { get { return Catalog.GetString ("Left click to draw freeform one-pixel wide lines with the primary color. Right click to use the secondary color."); } }
        public override Gdk.Cursor DefaultCursor { get { return new Gdk.Cursor (Gdk.Display.Default, PintaCore.Resources.GetIcon ("Cursor.Pencil.png"), 7, 24); } }
		public override Gdk.Key ShortcutKey { get { return Gdk.Key.P; } }
		public override int Priority { get { return 29; } }
		#endregion

		#region Mouse Handlers
        private PointD GetSnappedPoint (PointD start, PointD end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            
            // Avoid division by zero or jitter for very small movements
            if (Math.Abs(dx) < 1 && Math.Abs(dy) < 1) 
                return end;

            double angle = Math.Atan2 (dy, dx);
            double segment = Math.PI / 12; // 15 degrees (PI / 12)
            angle = Math.Round (angle / segment) * segment;
            
            double length = Math.Sqrt (dx * dx + dy * dy);
            
            return new PointD (start.X + length * Math.Cos (angle), start.Y + length * Math.Sin (angle));
        }

		protected override void OnMouseDown (Gtk.DrawingArea canvas, Gtk.ButtonPressEventArgs args, Cairo.PointD point)
		{
            if (mouse_button > 0)
                return;

            surface_modified = false;
            undo_surface = PintaCore.Workspace.ActiveDocument.CurrentUserLayer.Surface.Clone ();
            mouse_button = args.Event.Button;
            Color tool_color;

            if (mouse_button == 1)
                tool_color = PintaCore.Palette.PrimaryColor;
            else if (mouse_button == 3)
                tool_color = PintaCore.Palette.SecondaryColor;
            else
            {
                last_point = point_empty;
                return;
            }

            start_point_drawing = point;
            last_invalidated_rect = Gdk.Rectangle.Zero; // Reset trail tracker

            Draw (canvas, tool_color, point, true);
		}

		protected override void OnMouseMove (object o, Gtk.MotionNotifyEventArgs args, Cairo.PointD point)
		{
            Color tool_color;
            if (mouse_button == 1)
                tool_color = PintaCore.Palette.PrimaryColor;
            else if (mouse_button == 3)
                tool_color = PintaCore.Palette.SecondaryColor;
            else
            {
                last_point = point_empty;
                was_shift_held = false; // Reset tracker
                return;
            }

            var ev = args.Event;
            bool shiftHeld = false;
            bool ctrlHeld = false;

            if (ev != null && ev.Type == Gdk.EventType.MotionNotify) {
                Gdk.EventMotion motionEvent = (Gdk.EventMotion)ev;
                uint state = (uint)motionEvent.State;
                if ((state & 1) != 0) shiftHeld = true;
                if ((motionEvent.State & Gdk.ModifierType.ControlMask) != 0) ctrlHeld = true;
            }

            var doc = PintaCore.Workspace.ActiveDocument;

            // DETECT TRANSITIONS
            bool transition_to_shift = (!was_shift_held && shiftHeld);
            bool transition_to_normal = (was_shift_held && !shiftHeld);
            
            // Update tracker for next frame
            was_shift_held = shiftHeld;

            // SHIFT MODE
            if (shiftHeld) {
                ImageSurface surf = doc.CurrentUserLayer.Surface;
                PointD target_point = point;

                // 1. Fix Bug 1: If we just entered Shift mode, invalidate the whole screen.
                // This ensures the "clean" undo_surface is rendered immediately, 
                // wiping out the freehand scribbles that were on screen.
                if (transition_to_shift) {
                    doc.Workspace.Invalidate (); 
                }

                // 2. Angle Snapping Logic
                if (!ctrlHeld) {
                    target_point = GetSnappedPoint (start_point_drawing, point);
                }

                using (Context g = new Context (surf)) {
                    // Restore Clean Slate
                    g.SetSourceSurface (undo_surface, 0, 0);
                    g.Paint ();

                    // Setup Drawing
                    g.AppendPath (doc.Selection.SelectionPath);
                    g.FillRule = FillRule.EvenOdd;
                    g.Clip ();
                    g.Antialias = Antialias.None;
                    g.SetSourceColor (tool_color);
                    if (UseAlphaBlending)
                        g.SetBlendMode(BlendMode.Normal);
                    else
                        g.Operator = Operator.Source;
                    g.LineWidth = 1;
                    g.LineCap = LineCap.Square;

                    // Draw Line
                    int x = (int)target_point.X;
                    int y = (int) target_point.Y;
                    int startX = (int)start_point_drawing.X;
                    int startY = (int)start_point_drawing.Y;

                    g.MoveTo (startX + 0.5, startY + 0.5);
                    g.LineTo (x + 0.5, y + 0.5);
                    g.Stroke();
                    
                    // 3. Update Screen (inside using block)
                    Gdk.Rectangle current_rect = GetRectangleFromPoints (new Point(startX, startY), new Point(x, y));
                    
                    if (last_invalidated_rect != Gdk.Rectangle.Zero) {
                        doc.Workspace.Invalidate (last_invalidated_rect);
                    }
                    
                    doc.Workspace.Invalidate (current_rect);
                    last_invalidated_rect = current_rect;
                } 
            } 
            else {
                // NORMAL MODE
                
                // 4. Fix Bug 2: If we just left Shift mode, sync last_point to current mouse position.
                // This prevents the "V-Shape" caused by drawing from the old freehand location
                // to the current mouse location.
                if (transition_to_normal) {
                    last_point = new Point((int)point.X, (int)point.Y);
                }

                Draw ((DrawingArea) o, tool_color, point, false);
                last_invalidated_rect = Gdk.Rectangle.Zero;
            }
		}
		
		private void Draw (DrawingArea drawingarea1, Color tool_color, Cairo.PointD point, bool first_pixel)
		{
			int x = (int)point.X;
			int y = (int) point.Y;
			
			if (last_point.Equals (point_empty)) {
				last_point = new Point (x, y);
				
				if (!first_pixel)
					return;
			}
			
			Document doc = PintaCore.Workspace.ActiveDocument;

			if (doc.Workspace.PointInCanvas (point))
				surface_modified = true;

			ImageSurface surf = doc.CurrentUserLayer.Surface;
			
			using (Context g = new Context (surf)) {
				g.AppendPath (doc.Selection.SelectionPath);
				g.FillRule = FillRule.EvenOdd;
				g.Clip ();
				
				g.Antialias = Antialias.None;

				g.SetSourceColor(tool_color);
				if (UseAlphaBlending)
					g.SetBlendMode(BlendMode.Normal);
				else
					g.Operator = Operator.Source;
				g.LineWidth = 1;
				g.LineCap = LineCap.Square;

				if (first_pixel) {
					// Cairo does not support a single-pixel-long single-pixel-wide line
					g.Rectangle (x, y, 1.0, 1.0);
					g.Fill ();
                } else {
					// Adding 0.5 forces cairo into the correct square:
					// See https://bugs.launchpad.net/bugs/672232
					g.MoveTo (last_point.X + 0.5, last_point.Y + 0.5);
					g.LineTo (x + 0.5, y + 0.5);
					g.Stroke();
				}
			}
			
			Gdk.Rectangle r = GetRectangleFromPoints (last_point, new Point (x, y));

			doc.Workspace.Invalidate (doc.ClampToImageSize (r));
			
			last_point = new Point (x, y);
		}
		
		protected override void OnMouseUp (Gtk.DrawingArea canvas, Gtk.ButtonReleaseEventArgs args, Cairo.PointD point)
		{
			Document doc = PintaCore.Workspace.ActiveDocument;

            if (undo_surface != null)
            {
                if (surface_modified)
                    doc.History.PushNewItem(new SimpleHistoryItem(Icon, Name, undo_surface, doc.CurrentUserLayerIndex));
                else if (undo_surface != null)
                    (undo_surface as IDisposable).Dispose();
            }

			surface_modified = false;
            undo_surface = null;
            mouse_button = 0;
    
            // Cleanup trail tracker
            last_invalidated_rect = Gdk.Rectangle.Zero;
		}
		#endregion

		#region Private Methods
		private Gdk.Rectangle GetRectangleFromPoints (Point a, Point b)
		{
			int x = Math.Min (a.X, b.X) - 2 - 2;
			int y = Math.Min (a.Y, b.Y) - 2 - 2;
			int w = Math.Max (a.X, b.X) - x + (2 * 2) + 4;
			int h = Math.Max (a.Y, b.Y) - y + (2 * 2) + 4;
			
			return new Gdk.Rectangle (x, y, w, h);
		}
		#endregion
	}
}
