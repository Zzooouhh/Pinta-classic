// 
// EraserTool.cs
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
    public class EraserTool : BaseBrushTool
    {       
        private enum EraserType
        {
            Normal = 0,
            Smooth = 1,
        }

        private Point last_point = point_empty;
        private EraserType eraser_type = EraserType.Normal; 

        private const int LUT_Resolution = 256;
        private byte[][] lut_factor = null;

        private ToolBarLabel label_type = null;
        private ToolBarComboBox comboBox_type = null;

        public EraserTool ()
        {
        }

        private void initLookupTable()
        {
            if (lut_factor == null) {
                lut_factor = new byte[LUT_Resolution + 1][];

                for (int dy = 0; dy < LUT_Resolution+1; dy++) {
                    lut_factor [dy] = new byte[LUT_Resolution + 1];
                    for (int dx = 0; dx < LUT_Resolution+1; dx++) {
                        double d = Math.Sqrt (dx * dx + dy * dy) / LUT_Resolution;
                        if (d > 1.0)
                            lut_factor [dy][dx] = 255;
                        else
                            lut_factor [dy][dx] = (byte)(255.0 - Math.Cos (Math.Sqrt (d) * Math.PI / 2.0) * 255.0);
                    }
                }
            }
        }

        private ImageSurface copySurfacePart(ImageSurface surf, Gdk.Rectangle dest_rect)
        {
            ImageSurface tmp_surface = CairoExtensions.CreateImageSurface (Format.Argb32, dest_rect.Width, dest_rect.Height);

            using (Context g = new Context (tmp_surface)) {
                g.Operator = Operator.Source;
                g.SetSourceSurface (surf, -dest_rect.Left, -dest_rect.Top);
                g.Rectangle (new Rectangle (0, 0, dest_rect.Width, dest_rect.Height));
                g.Fill ();
            }
            //Flush to make sure all drawing operations are finished
            tmp_surface.Flush ();
            return tmp_surface;
        }

        private void pasteSurfacePart(Context g,ImageSurface tmp_surface, Gdk.Rectangle dest_rect)
        {       
            g.Operator = Operator.Source;
            g.SetSourceSurface (tmp_surface, dest_rect.Left, dest_rect.Top);
            g.Rectangle (new Rectangle (dest_rect.Left, dest_rect.Top, dest_rect.Width, dest_rect.Height));
            g.Fill ();
        }

        private void eraseNormal(Context g, PointD start, PointD end)
        {
            g.Antialias = UseAntialiasing ? Antialias.Subpixel : Antialias.None;

            // Adding 0.5 forces cairo into the correct square:
            // See https://bugs.launchpad.net/bugs/672232
            g.MoveTo (start.X + 0.5, start.Y + 0.5);
            g.LineTo (end.X + 0.5, end.Y + 0.5);

            // Right-click is erase to background color, left-click is transparent
            if (mouse_button == 3) {
                g.Operator = Operator.Source;
                g.SetSourceColor (PintaCore.Palette.SecondaryColor);
            }
            else
                g.Operator = Operator.Clear;

            g.LineWidth = BrushWidth;
            g.LineJoin = LineJoin.Round;
            g.LineCap = LineCap.Round;

            g.Stroke ();
        }

        protected unsafe void eraseSmooth(ImageSurface surf, Context g, PointD start, PointD end)
        {
            int rad = (int)(BrushWidth / 2.0) + 1;
            //Premultiply with alpha value
            byte bk_col_a = (byte)(PintaCore.Palette.SecondaryColor.A * 255.0);
            byte bk_col_r = (byte)(PintaCore.Palette.SecondaryColor.R * bk_col_a);
            byte bk_col_g = (byte)(PintaCore.Palette.SecondaryColor.G * bk_col_a);
            byte bk_col_b = (byte)(PintaCore.Palette.SecondaryColor.B * bk_col_a);
            int num_steps = (int)start.Distance(end) / rad + 1; 
            //Initialize lookup table when first used (to prevent slower startup of the application)
            initLookupTable ();

            for (int step = 0; step < num_steps; step++) {
                PointD pt = Utility.Lerp(start, end, (float)step / num_steps);
                int x = (int)pt.X, y = (int)pt.Y;

                Gdk.Rectangle surface_rect = new Gdk.Rectangle (0, 0, surf.Width, surf.Height);
                Gdk.Rectangle brush_rect = new Gdk.Rectangle (x - rad, y - rad, 2 * rad, 2 * rad);
                Gdk.Rectangle dest_rect = Gdk.Rectangle.Intersect (surface_rect, brush_rect);

                if ((dest_rect.Width > 0) && (dest_rect.Height > 0)) {
                    //Allow Clipping through a temporary surface
                    using (ImageSurface tmp_surface = copySurfacePart (surf, dest_rect)) {

                        for (int iy = dest_rect.Top; iy < dest_rect.Bottom; iy++) {
                            ColorBgra* srcRowPtr = tmp_surface.GetRowAddressUnchecked (iy - dest_rect.Top);
                            int dy = ((iy - y) * LUT_Resolution) / rad;
                            if (dy < 0)
                                dy = -dy;      
                            byte[] lut_factor_row = lut_factor [dy];

                            for (int ix = dest_rect.Left; ix < dest_rect.Right; ix++) {
                                ColorBgra col = *srcRowPtr;
                                int dx = ((ix - x) * LUT_Resolution) / rad;
                                if (dx < 0)
                                    dx = -dx;

                                int force = lut_factor_row [dx]; 
                                //Note: premultiplied alpha is used!
                                if (mouse_button == 3) {
                                    col.A = (byte)((col.A * force + bk_col_a * (255 - force)) / 255);         
                                    col.R = (byte)((col.R * force + bk_col_r * (255 - force)) / 255);
                                    col.G = (byte)((col.G * force + bk_col_g * (255 - force)) / 255);
                                    col.B = (byte)((col.B * force + bk_col_b * (255 - force)) / 255);
                                } else {
                                    col.A = (byte)(col.A * force / 255);
                                    col.R = (byte)(col.R * force / 255);
                                    col.G = (byte)(col.G * force / 255);
                                    col.B = (byte)(col.B * force / 255);
                                }
                                *srcRowPtr = col;
                                srcRowPtr++;
                            }
                        }
                        //Draw the final result on the surface
                        pasteSurfacePart (g, tmp_surface, dest_rect);
                    }
                }
            }
        }

        protected override void OnBuildToolBar(Toolbar tb)
        {
            base.OnBuildToolBar(tb);

            if (label_type == null)
                label_type = new ToolBarLabel (string.Format (" {0}: ", Catalog.GetString ("Type")));
            if (comboBox_type == null) {
                comboBox_type = new ToolBarComboBox (100, 0, false, Catalog.GetString ("Normal"), Catalog.GetString ("Smooth"));

                comboBox_type.ComboBox.Changed += (o, e) =>
                {
                    eraser_type = (EraserType)comboBox_type.ComboBox.Active;
                };
            }
            tb.AppendItem (label_type);
            tb.AppendItem (comboBox_type);
            // Change the cursor when the BrushWidth is changed.
            brush_width.ComboBox.Changed += (sender, e) => SetCursor (DefaultCursor);
        }

        #region Properties
        public override string Name { get { return Catalog.GetString ("Eraser"); } }
        public override string Icon { get { return "Tools.Eraser.png"; } }
        public override string StatusBarText { get { return Catalog.GetString ("Left click to erase to transparent, right click to erase to secondary color. "); } }

        public override Gdk.Cursor DefaultCursor {
            get {
                int iconOffsetX, iconOffsetY;
                var icon = CreateIconWithShape ("Cursor.Eraser.png",
                                                CursorShape.Ellipse, BrushWidth, 8, 22,
                                                out iconOffsetX, out iconOffsetY);
                return new Gdk.Cursor (Gdk.Display.Default, icon, iconOffsetX, iconOffsetY);
            }
        }
        public override bool CursorChangesOnZoom { get { return true; } }

        public override Gdk.Key ShortcutKey { get { return Gdk.Key.E; } }
        public override int Priority { get { return 27; } }
        #endregion

        #region Mouse Handlers
		protected override void OnMouseUp (DrawingArea canvas, ButtonReleaseEventArgs args, PointD point)
		{
            last_point = point_empty; //new Cairo.Point(-1, -1); // Use (-1,-1) as a sentinel (invalid) value
			base.OnMouseUp (canvas, args, point);
		}

        protected override void OnMouseMove (object o, Gtk.MotionNotifyEventArgs args, Cairo.PointD new_pointd)
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
        start_point_drawing = new_pointd;
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
        Cairo.PointD target_point = new_pointd;

        if (transition_to_shift) {
            PintaCore.Workspace.Invalidate (); // Fix ghosting
        }
        was_shift_held = true;

        if (!ctrlHeld) {
            target_point = GetSnappedPoint (start_point_drawing, new_pointd);
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
            g.Antialias = UseAntialiasing ? Antialias.Subpixel : Antialias.None;
            g.LineWidth = BrushWidth;
            g.LineCap = Cairo.LineCap.Round;
            g.LineJoin = Cairo.LineJoin.Round;

            // Special Check: Eraser Tool
            if (this.GetType ().Name == "EraserTool") {
                g.Operator = Cairo.Operator.DestOut;
                g.Color = new Cairo.Color (1, 1, 1, 1); // Full Alpha
            } else {
                // Normal Color logic
                if (mouse_button == 1)
                    g.SetSourceColor (PintaCore.Palette.PrimaryColor);
                else if (mouse_button == 3)
                    g.SetSourceColor (PintaCore.Palette.SecondaryColor);

                if (UseAlphaBlending) g.SetBlendMode(BlendMode.Normal);
                else g.Operator = Cairo.Operator.Source;
            }

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
            last_point = new Cairo.Point ((int)new_pointd.X, (int)new_pointd.Y);
        }

        // 2. Sentinel Check: Fix "Connecting Lines" and "Gaps"
        // If last_point is (-1,-1), we are at the start of a new stroke. Sync it.
        if (last_point.X == -500 && last_point.Y == -500) {
            last_point = new Cairo.Point ((int)new_pointd.X, (int)new_pointd.Y);
        }

        if (was_shift_held) {
            last_invalidated_rect = Gdk.Rectangle.Zero;
            was_shift_held = false;
        }
    }
            Point new_point = new Point ((int)new_pointd.X, (int)new_pointd.Y);

            Document doc = PintaCore.Workspace.ActiveDocument;

            if (mouse_button <= 0) {
                last_point = point_empty;
                return;
            }

            if (last_point.Equals (point_empty))
                last_point = new_point;

            if (doc.Workspace.PointInCanvas (new_pointd))
                surface_modified = true;

            var surf = doc.CurrentUserLayer.Surface;
            using (Context g = new Context (surf)) {

                g.AppendPath (doc.Selection.SelectionPath);
                g.FillRule = FillRule.EvenOdd;
                g.Clip ();
                PointD last_pointd = new PointD (last_point.X, last_point.Y);                

                if (eraser_type == EraserType.Normal) {
                    eraseNormal (g, last_pointd, new_pointd);
                }
                else if (eraser_type == EraserType.Smooth) {
                    eraseSmooth(surf, g, last_pointd, new_pointd);
                }
            }

            Gdk.Rectangle r = GetRectangleFromPoints (last_point, new_point);

            if (doc.Workspace.IsPartiallyOffscreen (r)) {
                doc.Workspace.Invalidate ();
            } else {
                doc.Workspace.Invalidate (doc.ClampToImageSize (r));
            }

            last_point = new_point;
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
