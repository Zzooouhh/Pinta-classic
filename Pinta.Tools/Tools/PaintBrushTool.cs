// 
// PaintBrushTool.cs
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

using Pinta.Tools.Brushes;

namespace Pinta.Tools
{
	public class PaintBrushTool : BaseBrushTool
	{
		#region Properties
		public override string Name { get { return Catalog.GetString ("Paintbrush"); } }
		public override string Icon { get { return "Tools.Paintbrush.png"; } }
		public override string StatusBarText { get { return Catalog.GetString ("Left click to draw with primary color, right click to draw with secondary color."); } }

		public override Gdk.Cursor DefaultCursor {
			get {
				int iconOffsetX, iconOffsetY;
				var icon = CreateIconWithShape ("Cursor.Paintbrush.png",
				                                CursorShape.Ellipse, BrushWidth, 8, 24,
				                                out iconOffsetX, out iconOffsetY);
                return new Gdk.Cursor (Gdk.Display.Default, icon, iconOffsetX, iconOffsetY);
			}
		}
		public override bool CursorChangesOnZoom { get { return true; } }

		public override Gdk.Key ShortcutKey { get { return Gdk.Key.B; } }
		public override int Priority { get { return 25; } }
		#endregion

		private BasePaintBrush default_brush;
		private BasePaintBrush active_brush;
		private ToolBarLabel brush_label;
		private ToolBarComboBox brush_combo_box;
		private Color stroke_color;
        private Point last_point = point_empty;

		protected override void OnActivated ()
		{
			base.OnActivated ();

			PintaCore.PaintBrushes.BrushAdded += HandleBrushAddedOrRemoved;
			PintaCore.PaintBrushes.BrushRemoved += HandleBrushAddedOrRemoved;
		}

		protected override void OnDeactivated (BaseTool newTool)
		{
			base.OnDeactivated (newTool);

			PintaCore.PaintBrushes.BrushAdded -= HandleBrushAddedOrRemoved;
			PintaCore.PaintBrushes.BrushRemoved -= HandleBrushAddedOrRemoved;
		}

		protected override void OnBuildToolBar (Toolbar tb)
		{
			base.OnBuildToolBar (tb);

			// Change the cursor when the BrushWidth is changed.
			brush_width.ComboBox.Changed += (sender, e) => SetCursor (DefaultCursor);

			tb.AppendItem (new Gtk.SeparatorToolItem ());

			if (brush_label == null)
				brush_label = new ToolBarLabel (string.Format (" {0}:  ", Catalog.GetString ("Type")));

			if (brush_combo_box == null) {
				brush_combo_box = new ToolBarComboBox (100, 0, false);
				brush_combo_box.ComboBox.Changed += (o, e) => {
					Gtk.TreeIter iter;
					if (brush_combo_box.ComboBox.GetActiveIter (out iter)) {
						active_brush = (BasePaintBrush)brush_combo_box.Model.GetValue (iter, 1);
					} else {
						active_brush = default_brush;
					}
				};

				RebuildBrushComboBox ();
			}

			tb.AppendItem (brush_label);
			tb.AppendItem (brush_combo_box);
		}

		/// <summary>
		/// Rebuild the list of brushes when a brush is added or removed.
		/// </summary>
		private void HandleBrushAddedOrRemoved (object sender, BrushEventArgs e)
		{
			RebuildBrushComboBox ();
		}

		/// <summary>
		/// Rebuild the list of brushes.
		/// </summary>
		private void RebuildBrushComboBox ()
		{
			brush_combo_box.Model.Clear ();
			default_brush = null;

			foreach (var brush in PintaCore.PaintBrushes) {
				if (default_brush == null)
					default_brush = (BasePaintBrush)brush;
				brush_combo_box.Model.AppendValues (brush.Name, brush);
			}

			brush_combo_box.ComboBox.Active = 0;
		}

		#region Mouse Handlers
		protected override void OnMouseDown (DrawingArea canvas, ButtonPressEventArgs args, PointD point)
		{
            base.OnMouseDown (canvas, args, point);
            active_brush.DoMouseDown ();
		}

		protected override void OnMouseUp (DrawingArea canvas, ButtonReleaseEventArgs args, PointD point)
		{
            last_point = point_empty; //new Cairo.Point(-1, -1); // Use (-1,-1) as a sentinel (invalid) value
			base.OnMouseUp (canvas, args, point);
			active_brush.DoMouseUp ();
		}

		protected override void OnMouseMove (object o, Gtk.MotionNotifyEventArgs args, Cairo.PointD point)
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

			if (mouse_button == 1) {
				stroke_color = PintaCore.Palette.PrimaryColor;
			} else if (mouse_button == 3) {
				stroke_color = PintaCore.Palette.SecondaryColor;
			} else {
				last_point = point_empty;
				return;
			}

			// TODO: also multiply by pressure
			stroke_color = new Color (stroke_color.R, stroke_color.G, stroke_color.B,
				stroke_color.A * active_brush.StrokeAlphaMultiplier);

			int x = (int)point.X;
			int y = (int)point.Y;

			if (last_point.Equals (point_empty))
				last_point = new Point (x, y);

			if (doc.Workspace.PointInCanvas (point))
				surface_modified = true;

			var surf = doc.CurrentUserLayer.Surface;
			var invalidate_rect = Gdk.Rectangle.Zero;
			var brush_width = BrushWidth;

			using (var g = new Context (surf)) {
				g.AppendPath (doc.Selection.SelectionPath);
				g.FillRule = FillRule.EvenOdd;
				g.Clip ();

				g.Antialias = UseAntialiasing ? Antialias.Subpixel : Antialias.None;
				g.LineWidth = brush_width;
				g.LineJoin = LineJoin.Round;
				g.LineCap = BrushWidth == 1 ? LineCap.Butt : LineCap.Round;
				g.SetSourceColor (stroke_color);

                invalidate_rect = active_brush.DoMouseMove (g, stroke_color, surf,
				                                            x, y, last_point.X, last_point.Y);
			}

			// If we draw partially offscreen, Cairo gives us a bogus
			// dirty rectangle, so redraw everything.
			if (doc.Workspace.IsPartiallyOffscreen (invalidate_rect)) {
				doc.Workspace.Invalidate ();
			} else {
				doc.Workspace.Invalidate (doc.ClampToImageSize (invalidate_rect));
			}

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
	}
}
