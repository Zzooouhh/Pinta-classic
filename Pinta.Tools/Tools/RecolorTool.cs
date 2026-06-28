// 
// RecolorTool.cs
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

// Some methods from Paint.Net:

/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
/////////////////////////////////////////////////////////////////////////////////

using System;
using Cairo;
using Gtk;
using Pinta.Core;
using Mono.Unix;

namespace Pinta.Tools
{
	public class RecolorTool : BaseBrushTool
	{
		protected ToolBarLabel tolerance_label;
		protected ToolBarSlider tolerance_slider;
		
		private Point last_point = point_empty;
		private bool[,] stencil;
		private int myTolerance;

		public RecolorTool ()
		{
		}

		#region Properties
		public override string Name { get { return Catalog.GetString ("Recolor"); } }
		public override string Icon { get { return "Tools.Recolor.png"; } }
		public override string StatusBarText {
			get {
				return Catalog.GetString ("Left click to replace the secondary color with the primary color. " +
				                          "Right click to reverse.");
			}
		}
        public override Gdk.Cursor DefaultCursor { get { return new Gdk.Cursor (Gdk.Display.Default, PintaCore.Resources.GetIcon ("Cursor.Recolor.png"), 9, 18); } }
		public override Gdk.Key ShortcutKey { get { return Gdk.Key.R; } }
		protected float Tolerance { get { return (float)(tolerance_slider.Slider.Value / 100); } }
		public override int Priority { get { return 35; } }
		#endregion

		#region ToolBar
		protected override void OnBuildToolBar (Gtk.Toolbar tb)
		{
			base.OnBuildToolBar (tb);

			tb.AppendItem (new Gtk.SeparatorToolItem ());

			if (tolerance_label == null)
				tolerance_label = new ToolBarLabel (string.Format ("  {0}: ", Catalog.GetString ("Tolerance")));

			tb.AppendItem (tolerance_label);

			if (tolerance_slider == null)
				tolerance_slider = new ToolBarSlider (0, 100, 1, 50);

			tb.AppendItem (tolerance_slider);
		}
		#endregion

        protected override void OnKeyDown(Gtk.DrawingArea canvas, Gtk.KeyPressEventArgs args)
        {
			Gdk.Key keyPressed = args.Event.Key;

            if ((args.Event.State & Gdk.ModifierType.ControlMask) != 0)
            {
                if (keyPressed == Gdk.Key.bracketleft || keyPressed == Gdk.Key.braceleft)
                {
                    if ((args.Event.State & Gdk.ModifierType.ShiftMask) != Gdk.ModifierType.ShiftMask)
                        if (Tolerance > 0.10)
                            tolerance_slider.Slider.Value -= 10;
                        else
                            tolerance_slider.Slider.Value = 0;
                    else if (Tolerance > 0)
                            tolerance_slider.Slider.Value--;
                    args.RetVal = true;
                    return;
                }
                else if (keyPressed == Gdk.Key.bracketright || keyPressed == Gdk.Key.braceright)
                {
                    if ((args.Event.State & Gdk.ModifierType.ShiftMask) != Gdk.ModifierType.ShiftMask)
                        if (Tolerance < 0.9)
                            tolerance_slider.Slider.Value += 10;
                        else
                            tolerance_slider.Slider.Value = 100;
                    else if (Tolerance < 1)
                            tolerance_slider.Slider.Value++;
                    args.RetVal = true;
                    return;
                }
            }
            base.OnKeyDown(canvas, args);
        }

		#region Mouse Handlers
		protected override void OnMouseDown (DrawingArea canvas, ButtonPressEventArgs args, PointD point)
		{
			Document doc = PintaCore.Workspace.ActiveDocument;

			doc.ToolLayer.Clear ();
			stencil = new bool[doc.ImageSize.Width, doc.ImageSize.Height];

			base.OnMouseDown (canvas, args, point);
		}

		protected override void OnMouseUp (DrawingArea canvas, ButtonReleaseEventArgs args, PointD point)
		{
			base.OnMouseUp (canvas, args, point);
		}
		
		protected unsafe override void OnMouseMove (object o, Gtk.MotionNotifyEventArgs args, Cairo.PointD point)
		{
			// --- SAFETY CHECK ---
			// If undo_surface wasn't initialized (e.g. right click or quick switch), stop.
			if (undo_surface == null) return;
			
			// If the document/layer is invalid, stop.
			if (PintaCore.Workspace.ActiveDocument == null || PintaCore.Workspace.ActiveDocument.CurrentUserLayer == null) return;
			// --------------------
						// --- SHIFT STRAIGHT LINE LOGIC ---
			// 1. Initialization Check (Structs cannot be null, so check 0,0)
			if (start_point_drawing.X == 0 && start_point_drawing.Y == 0) {
				start_point_drawing = point;
				last_invalidated_rect = Gdk.Rectangle.Zero;
		}

    var ev = args != null ? args.Event : null;
    
    bool shiftHeld = false;
    if (ev != null && ev.Type == Gdk.EventType.MotionNotify) {
        Gdk.EventMotion motionEvent = (Gdk.EventMotion)ev;
        // Check raw bit 1 for Shift
        if ((((uint)motionEvent.State) & 1) != 0) {
            shiftHeld = true;
        }
    }
    
    bool ctrlHeld = false;
    if (ev != null && ev.Type == Gdk.EventType.MotionNotify) {
        Gdk.EventMotion motionEvent = (Gdk.EventMotion)ev;
        if ((motionEvent.State & Gdk.ModifierType.ControlMask) != 0) {
            ctrlHeld = true;
        }
    }

    bool transition_to_shift = (!was_shift_held && shiftHeld);
    bool transition_to_normal = (was_shift_held && !shiftHeld);
    was_shift_held = shiftHeld;

    if (shiftHeld) {
        // FIX: Mark surface as modified so Undo works for Shift strokes
        surface_modified = true;

        // Use FULL namespaces and UNIQUE names to avoid conflicts
        Cairo.ImageSurface gSurf = PintaCore.Workspace.ActiveDocument.CurrentUserLayer.Surface;
        Cairo.PointD target_point = point;

        if (transition_to_shift) {
            PintaCore.Workspace.Invalidate (); // Fix ghosting
        }
        was_shift_held = true;

        if (!ctrlHeld) {
            target_point = GetSnappedPoint (start_point_drawing, point);
        }

        using (Cairo.Context g = new Cairo.Context (gSurf)) {
            g.Save();
            g.Operator = Cairo.Operator.Source; 
            g.SetSourceSurface (undo_surface, 0, 0);
            g.Paint ();
            g.Restore(); // Restore previous state (for the Stroke)
            // ----------------------

            // Setup Drawing
            g.AppendPath (PintaCore.Workspace.ActiveDocument.Selection.SelectionPath);
            g.FillRule = Cairo.FillRule.EvenOdd;
            g.Clip ();
            // --- FIX ANTI-ALIASING ARTIFACTS ---
            // Switching to Antialias.None prevents "Bleeding" pixels 
            // and ensures the line aligns perfectly with the restored pixels.
            // You can change this to .Default if it looks too jagged, 
            // but .None fixes the "erased lines recede" bug.
            g.Antialias = UseAntialiasing ? Antialias.Default : Antialias.None;
            g.LineWidth = BrushWidth;
            g.LineCap = Cairo.LineCap.Round;
            g.LineJoin = Cairo.LineJoin.Round;

            // Color check
            if (mouse_button == 1)
                g.SetSourceColor (PintaCore.Palette.PrimaryColor);
            else if (mouse_button == 3)
                g.SetSourceColor (PintaCore.Palette.SecondaryColor);

            if (UseAlphaBlending) g.SetBlendMode(BlendMode.Normal);
            else g.Operator = Cairo.Operator.Source;

            g.MoveTo (start_point_drawing.X, start_point_drawing.Y);
            g.LineTo (target_point.X, target_point.Y);
            g.Stroke();
            
            // Update Screen
            int x1 = (int) Math.Min(start_point_drawing.X, target_point.X);
            int y1 = (int) Math.Min(start_point_drawing.Y, target_point.Y);
            int x2 = (int) Math.Max(start_point_drawing.X, target_point.X);
            int y2 = (int) Math.Max(start_point_drawing.Y, target_point.Y);
            int pad = (int)(BrushWidth / 2) + 2;
            Gdk.Rectangle current_rect = new Gdk.Rectangle (x1 - pad, y1 - pad, (x2 - x1) + pad * 2, (y2 - y1) + pad * 2);

            if (last_invalidated_rect != Gdk.Rectangle.Zero) PintaCore.Workspace.Invalidate (last_invalidated_rect);
            PintaCore.Workspace.Invalidate (current_rect);
            last_invalidated_rect = current_rect;
        }
        return; // STOP! Don't run the original tool logic
    } else {
        // Fix for Dotted Lines: ONLY sync 'last_point' when we transition from Shift to Normal.
        // Do NOT update it every frame, or we break the drawing continuity.
        if (transition_to_normal) {
            // Cast logic depending on the tool
            last_point = new Cairo.Point ((int)point.X, (int)point.Y);
        }

        // 2. Sentinel Check: Fix "Connecting Lines" and "Gaps"
        // If last_point is (-1,-1), we are at the start of a new stroke. Sync it.
        if (last_point.X == -500 && last_point.Y == -500) {
            last_point = new Cairo.Point ((int)point.X, (int)point.Y);
        }

        if (was_shift_held) {
            last_invalidated_rect = Gdk.Rectangle.Zero;
            was_shift_held = false;
        }
    }
			Document doc = PintaCore.Workspace.ActiveDocument;

			ColorBgra old_color;
			ColorBgra new_color;
			
			if (mouse_button == 1) {
				old_color = PintaCore.Palette.PrimaryColor.ToColorBgra ();
				new_color = PintaCore.Palette.SecondaryColor.ToColorBgra ();
			} else if (mouse_button == 3) {
				old_color = PintaCore.Palette.SecondaryColor.ToColorBgra ();
				new_color = PintaCore.Palette.PrimaryColor.ToColorBgra ();
			} else {
				last_point = point_empty;
				return;
			}
				
			int x = (int)point.X;
			int y = (int)point.Y;
			
			if (last_point.Equals (point_empty))
				last_point = new Point (x, y);

			if (doc.Workspace.PointInCanvas (point))
				surface_modified = true;

			ImageSurface surf = doc.CurrentUserLayer.Surface;
			ImageSurface tmp_layer = doc.ToolLayer.Surface;

			Gdk.Rectangle roi = GetRectangleFromPoints (last_point, new Point (x, y));

			roi = PintaCore.Workspace.ClampToImageSize (roi);
			myTolerance = (int)(Tolerance * 256);
			
			tmp_layer.Flush ();

			ColorBgra* tmp_data_ptr = (ColorBgra*)tmp_layer.DataPtr;
			int tmp_width = tmp_layer.Width;
			ColorBgra* surf_data_ptr = (ColorBgra*)surf.DataPtr;
			int surf_width = surf.Width;
			
			// The stencil lets us know if we've already checked this
			// pixel, providing a nice perf boost
			// Maybe this should be changed to a BitVector2DSurfaceAdapter?
			for (int i = roi.X; i <= roi.GetRight (); i++)
				for (int j = roi.Y; j <= roi.GetBottom (); j++) {
					if (stencil[i, j])
						continue;
						
					if (IsColorInTolerance (new_color, surf.GetColorBgraUnchecked (surf_data_ptr, surf_width, i, j)))
						*tmp_layer.GetPointAddressUnchecked (tmp_data_ptr, tmp_width, i, j) = AdjustColorDifference (new_color, old_color, surf.GetColorBgraUnchecked (surf_data_ptr, surf_width, i, j));

					stencil[i, j] = true;
				}
			
			tmp_layer.MarkDirty ();

			using (Context g = new Context (surf)) {
				g.AppendPath (doc.Selection.SelectionPath);
				g.FillRule = FillRule.EvenOdd;
				g.Clip ();

				g.Antialias = UseAntialiasing ? Antialias.Subpixel : Antialias.None;
				
				g.MoveTo (last_point.X, last_point.Y);
				g.LineTo (x, y);

				g.LineWidth = BrushWidth;
				g.LineJoin = LineJoin.Round;
				g.LineCap = LineCap.Round;
				
				g.SetSource (tmp_layer);
				
				g.Stroke ();
			}

			doc.Workspace.Invalidate (roi);
			
			last_point = new Point (x, y);
		}

        private PointD GetSnappedPoint (PointD start, PointD end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            
            if (Math.Abs(dx) < 1 && Math.Abs(dy) < 1) 
                return end;

            double angle = Math.Atan2 (dy, dx);
            double segment = Math.PI / 12;
            angle = Math.Round (angle / segment) * segment;
            
            double length = Math.Sqrt (dx * dx + dy * dy);
            
            return new PointD (start.X + length * Math.Cos (angle), start.Y + length * Math.Sin (angle));
        }
		#endregion

		#region Private PDN Methods
		private bool IsColorInTolerance (ColorBgra colorA, ColorBgra colorB)
		{
			return Utility.ColorDifference (colorA, colorB) <= myTolerance;
		}

		private static bool CheckColor (ColorBgra a, ColorBgra b, int tolerance)
		{
			int sum = 0;
			int diff;

			diff = a.R - b.R;
			sum += (1 + diff * diff) * a.A / 256;

			diff = a.G - b.G;
			sum += (1 + diff * diff) * a.A / 256;

			diff = a.B - b.B;
			sum += (1 + diff * diff) * a.A / 256;

			diff = a.A - b.A;
			sum += diff * diff;

			return (sum <= tolerance * tolerance * 4);
		}

		private ColorBgra AdjustColorDifference (ColorBgra oldColor, ColorBgra newColor, ColorBgra basisColor)
		{
			ColorBgra returnColor;

			// eliminate testing for the "equal to" case
			returnColor = basisColor;

			returnColor.B = AdjustColorByte (oldColor.B, newColor.B, basisColor.B);
			returnColor.G = AdjustColorByte (oldColor.G, newColor.G, basisColor.G);
			returnColor.R = AdjustColorByte (oldColor.R, newColor.R, basisColor.R);

			return returnColor;
		}
		private byte AdjustColorByte (byte oldByte, byte newByte, byte basisByte)
		{
			if (oldByte > newByte)
				return Utility.ClampToByte (basisByte - (oldByte - newByte));
			else
				return Utility.ClampToByte (basisByte + (newByte - oldByte));
		}
		#endregion
	}
}
