// 
// SelectTool.cs
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
using System.Linq;
using Gdk;
using System.Collections.Generic;
using ClipperLibrary;

namespace Pinta.Tools
{
	public abstract class SelectTool : SelectShapeTool
	{
		private PointD reset_origin;
		private PointD shape_end;
		private ToolControl [] controls = new ToolControl [8];
        private int? active_control;
		private SelectionHistoryItem hist;
		public override Gdk.Key ShortcutKey { get { return Gdk.Key.S; } }
		protected override bool ShowAntialiasingButton { get { return false; } }
	    private CursorType? active_cursor;
        private CombineMode combine_mode;

		public SelectTool ()
		{
			CreateHandler ();

		    PintaCore.Workspace.SelectionChanged += AfterSelectionChange;
		    PintaCore.Workspace.ActiveDocumentChanged += (s, e) => 
			{
				if (!PintaCore.Workspace.HasOpenDocuments)
					return;

				var doc = PintaCore.Workspace.ActiveDocument;
				doc.ToolLayer.Clear();
			};
		}

	    #region ToolBar
		// We don't want the ShapeTool's toolbar
		protected override void BuildToolBar (Toolbar tb)
		{
            PintaCore.Workspace.SelectionHandler.BuildToolbar (tb);
		}
		#endregion

        protected override void OnKeyDown (Gtk.DrawingArea canvas, Gtk.KeyPressEventArgs args)
        {
            if (mouse_button != 1 && mouse_button != 3) {
                base.OnKeyDown (canvas, args);
                return;
            }
            bool ctrl = (args.Event.State & Gdk.ModifierType.ControlMask) != 0;
            int delta = ctrl ? 10 : 1;

            switch (args.Event.Key) {
                case Gdk.Key.Left:
                    current_point.X -= delta;
                    break;
                case Gdk.Key.Right:
                    current_point.X += delta;
                    break;
                case Gdk.Key.Up:
                    current_point.Y -= delta;
                    break;
                case Gdk.Key.Down:
                    current_point.Y += delta;
                    break;
                default:
                    base.OnKeyDown (canvas, args);
                    return;
            }

            var doc = PintaCore.Workspace.ActiveDocument;
            // --- REPLICATE MOUSE STATE ---
            // 'args.Event.State' only has Shift/Ctrl. We must add the Mouse Button state
            // so HandleMouseMove thinks we are still dragging.

            // A. Clamp Coordinates
            double x = Utility.Clamp (current_point.X, 0, doc.ImageSize.Width - 1);
            double y = Utility.Clamp (current_point.Y, 0, doc.ImageSize.Height - 1);

            // B. Update the Active Control Handle (This is what actually resizes the shape)
            controls[active_control.Value].HandleMouseMove (x, y, args.Event.State);

            // C. Redraw Visuals
            ReDraw (args.Event.State);

            // D. Update the Selection Data (Marching Ants)
            if (doc.Selection != null) {
                SelectionModeHandler.PerformSelectionMode (combine_mode, doc.Selection.SelectionPolygons);
                PintaCore.Workspace.Invalidate();
            }
            return; // Don't pass to base
        }

		#region Mouse Handlers
		protected override void OnMouseDown (DrawingArea canvas, ButtonPressEventArgs args, Cairo.PointD point)
		{
			// Ignore extra button clicks while drawing
			if (is_drawing)
				return;

			Document doc = PintaCore.Workspace.ActiveDocument;
			hist = new SelectionHistoryItem(Icon, Name);
			hist.TakeSnapshot();

			reset_origin = args.Event.GetPoint();

            active_control = HandleResize (point);
			if (!active_control.HasValue)
			{
				combine_mode = PintaCore.Workspace.SelectionHandler.DetermineCombineMode(args);

				double x = Utility.Clamp(point.X, 0, doc.ImageSize.Width - 1);
				double y = Utility.Clamp(point.Y, 0, doc.ImageSize.Height - 1);
				shape_origin = point;
				shape_end = point;
                current_point = point;

                doc.PreviousSelection.Dispose ();
				doc.PreviousSelection = doc.Selection.Clone();
				doc.Selection.SelectionPolygons.Clear();

                // The bottom right corner should be selected.
                active_control = 3;
			}
            canvas.GrabFocus ();
            mouse_button = args.Event.Button;
            is_drawing = true;
		}
		
		protected override void OnMouseUp (DrawingArea canvas, ButtonReleaseEventArgs args, Cairo.PointD point)
		{
			Document doc = PintaCore.Workspace.ActiveDocument;

			// If the user didn't move the mouse, they want to deselect
			int tolerance = 0;
			if (Math.Abs (reset_origin.X - args.Event.X) <= tolerance && Math.Abs (reset_origin.Y - args.Event.Y) <= tolerance) {
				// Mark as being done interactive drawing before invoking the deselect action.
				// This will allow AfterSelectionChanged() to clear the selection.
				is_drawing = false;

                if (hist != null)
                {
					// Roll back any changes made to the selection, e.g. in OnMouseDown().
					hist.Undo ();

                    hist.Dispose();
                    hist = null;
                }

				PintaCore.Actions.Edit.Deselect.Activate ();

			} else {
                ClearHandles (doc.ToolLayer);
				ReDraw(args.Event.State);
				if (doc.Selection != null)
				{

					var rect = Utility.PointsToRectangle(shape_origin, shape_end, false);
					DrawShape(rect, doc.SelectionLayer);

					doc.Selection.SelectionPolygons = ClipToCanvas(doc.Selection.SelectionPolygons);

					SelectionModeHandler.PerformSelectionMode(combine_mode, doc.Selection.SelectionPolygons);
					UpdateHandler();

					doc.Selection.Origin = shape_origin;
					doc.Selection.End = shape_end;

					PintaCore.Workspace.Invalidate();
				}
                if (hist != null)
                {
                    doc.History.PushNewItem(hist);
                    hist = null;
                }
			}

            mouse_button = 0;
			is_drawing = false;
            active_control = null;

            // Update the mouse cursor.
            UpdateCursor (point);
		}

        protected override void OnActivated()
        {
            base.OnActivated();

			if (PintaCore.Workspace.HasOpenDocuments)
            {
				PintaCore.Workspace.ActiveWorkspace.CanvasInvalidated += OnCanvasInvalidated;

				var doc = PintaCore.Workspace.ActiveDocument;
				shape_origin = doc.Selection.Origin;
				shape_end = doc.Selection.End;
				UpdateHandler();
			}
        }

        protected override void OnDeactivated(BaseTool newTool)
		{
			base.OnDeactivated (newTool);

			PintaCore.Workspace.ActiveWorkspace.CanvasInvalidated -= OnCanvasInvalidated;

			if (PintaCore.Workspace.HasOpenDocuments) {
				Document doc = PintaCore.Workspace.ActiveDocument;
				doc.ToolLayer.Clear ();
			}
		}

		protected override void OnMouseMove (object o, MotionNotifyEventArgs args, Cairo.PointD point)
		{
			Document doc = PintaCore.Workspace.ActiveDocument;

			if (!is_drawing)
			{
                UpdateCursor (point);
                return;
			}

            controls[active_control.Value].HandleMouseMove (point.X, point.Y, args.Event.State);
            current_point = point;

            ReDraw (args.Event.State);
			
			if (doc.Selection != null)
			{
			    SelectionModeHandler.PerformSelectionMode (combine_mode, ClipToCanvas(doc.Selection.SelectionPolygons));
				PintaCore.Workspace.Invalidate();
			}
		}

		protected void RefreshHandler ()
		{
			Document doc = PintaCore.Workspace.ActiveDocument;

            double originX = Utility.Clamp(shape_origin.X, 0, doc.ImageSize.Width - 1);
            double originY = Utility.Clamp(shape_origin.Y, 0, doc.ImageSize.Height - 1);
            double endX = Utility.Clamp(shape_end.X, 0, doc.ImageSize.Width - 1);
            double endY = Utility.Clamp(shape_end.Y, 0, doc.ImageSize.Height - 1);
            
			controls[0].Position = new PointD (originX, originY);
			controls[1].Position = new PointD (originX, endY);
			controls[2].Position = new PointD (endX, originY);
			controls[3].Position = new PointD (endX, endY);
			controls[4].Position = new PointD (originX, (originY + endY) / 2);
			controls[5].Position = new PointD ((originX + endX) / 2, originY);
			controls[6].Position = new PointD (endX, (originY + endY) / 2);
			controls[7].Position = new PointD ((originX + endX) / 2, endY);
		}

		public void ReDraw (Gdk.ModifierType state)
		{
			Document doc = PintaCore.Workspace.ActiveDocument;

			doc.Selection.Visible = true;
			doc.ToolLayer.Hidden = false;
			bool constraint = (state & Gdk.ModifierType.ShiftMask) != 0;

			Cairo.Rectangle rect = Utility.PointsToRectangle (shape_origin, shape_end, constraint);
			Cairo.Rectangle dirty = DrawShape (rect, doc.SelectionLayer);

            DrawHandler (doc.ToolLayer);

			last_dirty = dirty;
		}

		protected void CreateHandler ()
		{
			controls[0] = new ToolControl (CursorType.TopLeftCorner, (x, y, s) => {
				shape_origin.X = x;
				shape_origin.Y = y;
				if ((s & Gdk.ModifierType.ShiftMask) != 0) {
					PointD moving = new PointD(shape_origin.X, shape_origin.Y);
					PointD anchor = new PointD(shape_end.X, shape_end.Y);

					ConstrainSquare(ref moving, anchor);

					shape_origin.X = moving.X;
					shape_origin.Y = moving.Y;
				}
			});
			controls[1] = new ToolControl (CursorType.BottomLeftCorner, (x, y, s) => {
				shape_origin.X = x;
				shape_end.Y = y;
				if ((s & Gdk.ModifierType.ShiftMask) != 0) {
					PointD moving = new PointD(shape_origin.X, shape_end.Y);
					PointD anchor = new PointD(shape_end.X, shape_origin.Y);

					ConstrainSquare(ref moving, anchor);

					shape_origin.X = moving.X;
					shape_end.Y = moving.Y;
				}
			});
			controls[2] = new ToolControl (CursorType.TopRightCorner, (x, y, s) => {
				shape_end.X = x;
				shape_origin.Y = y;
				if ((s & Gdk.ModifierType.ShiftMask) != 0) {
					PointD moving = new PointD(shape_end.X, shape_origin.Y);
					PointD anchor = new PointD(shape_origin.X, shape_end.Y);

					ConstrainSquare(ref moving, anchor);

					shape_end.X = moving.X;
					shape_origin.Y = moving.Y;
				}
			});
			controls[3] = new ToolControl (CursorType.BottomRightCorner, (x, y, s) => {
				shape_end.X = x;
				shape_end.Y = y;
				if ((s & Gdk.ModifierType.ShiftMask) != 0) {
					PointD moving = new PointD(shape_end.X, shape_end.Y);
					PointD anchor = new PointD(shape_origin.X, shape_origin.Y);

					ConstrainSquare(ref moving, anchor);

					shape_end.X = moving.X;
					shape_end.Y = moving.Y;
				}
			});
			controls[4] = new ToolControl (CursorType.LeftSide, (x, y, s) => {
				shape_origin.X = x;
				if ((s & Gdk.ModifierType.ShiftMask) != 0) {
					double d = shape_end.X - shape_origin.X;
					double mid = (shape_origin.Y + shape_end.Y) / 2;

					shape_origin.Y = mid - d / 2;
					shape_end.Y = mid + d / 2;
				}
			});
			controls[5] = new ToolControl (CursorType.TopSide, (x, y, s) => {
				shape_origin.Y = y;
				if ((s & Gdk.ModifierType.ShiftMask) != 0) {
					double d = shape_end.Y - shape_origin.Y;
					double mid = (shape_origin.X + shape_end.X) / 2;

					shape_origin.X = mid - d / 2;
					shape_end.X = mid + d / 2;
				}
			});
			controls[6] = new ToolControl (CursorType.RightSide, (x, y, s) => {
				shape_end.X = x;
				if ((s & Gdk.ModifierType.ShiftMask) != 0) {
					double d = shape_end.X - shape_origin.X;
					double mid = (shape_origin.Y + shape_end.Y) / 2;

					shape_origin.Y = mid - d / 2;
					shape_end.Y = mid + d / 2;
				}
			});
			controls[7] = new ToolControl (CursorType.BottomSide, (x, y, s) => {
				shape_end.Y = y;
				if ((s & Gdk.ModifierType.ShiftMask) != 0) {
					double d = shape_end.Y - shape_origin.Y;
					double mid = (shape_origin.X + shape_end.X) / 2;

					shape_origin.X = mid - d / 2;
					shape_end.X = mid + d / 2;
				}
			});
		}

		public int? HandleResize (PointD point)
		{
            for (int i = 0; i < controls.Length; ++i)
            {
                if (controls[i].IsInside (point))
                    return i;
            }

			return null;
		}

		public void DrawHandler (Layer layer)
		{
			if (!PintaCore.Workspace.ActiveDocument.Selection.Visible)
				return;

		    using (var g = new Context(layer.Surface))
		    {
		        foreach (var tool_control in controls)
                    tool_control.Render (g);
		    }
		}

		public void UpdateCursor (PointD point)
		{
			if (PintaCore.Workspace.ActiveDocument.Selection.Visible)
            {
				foreach (ToolControl ct in controls.Where(ct => ct.IsInside(point)))
				{
					if (active_cursor != ct.Cursor)
					{
						SetCursor(new Cursor(ct.Cursor));
						active_cursor = ct.Cursor;
					}
					return;
				}
			}

		    if (active_cursor.HasValue)
            {
				SetCursor (DefaultCursor);
                active_cursor = null;
            }
		}

	    #endregion

		public override void AfterUndo()
		{
			base.AfterUndo();

			Document doc = PintaCore.Workspace.ActiveDocument;

            if (PintaCore.Tools.CurrentTool == this)
                doc.ToolLayer.Hidden = false;

			shape_origin = doc.Selection.Origin;
			shape_end = doc.Selection.End;
			UpdateHandler();
		}

		public override void AfterRedo()
		{
			base.AfterRedo();

			Document doc = PintaCore.Workspace.ActiveDocument;

            if (PintaCore.Tools.CurrentTool == this)
                doc.ToolLayer.Hidden = false;

			shape_origin = doc.Selection.Origin;
			shape_end = doc.Selection.End;
			UpdateHandler();
		}

	    private void AfterSelectionChange (object sender, EventArgs event_args)
	    {
	        if (is_drawing || !PintaCore.Workspace.HasOpenDocuments)
	            return;

	        var selection = PintaCore.Workspace.ActiveDocument.Selection;
	        shape_origin = selection.Origin;
	        shape_end = selection.End;

            if (PintaCore.Tools.CurrentTool == this)
                UpdateHandler();
        }

		private void OnCanvasInvalidated(object sender, CanvasInvalidatedEventArgs e)
		{
			var doc = PintaCore.Workspace.ActiveDocument;

			doc.ToolLayer.Clear();

			RefreshHandler();
			DrawHandler(doc.ToolLayer);
		}

		private List<List<IntPoint>> ClipToCanvas(List<List<IntPoint>> subject)
		{
			var doc = PintaCore.Workspace.ActiveDocument;

			// Convert selection polygon(s) to Clipper points
			List<List<IntPoint>> subjectPolygons = doc.Selection.SelectionPolygons
				.Select(polygon => polygon.Select(pt => new IntPoint((long)pt.X, (long)pt.Y)).ToList())
				.ToList();

			// Define canvas rectangle as polygon
			List<List<IntPoint>> clipPolygons = new List<List<IntPoint>>() {
				new List<IntPoint>() {
					new IntPoint(0, 0),
					new IntPoint(doc.ImageSize.Width, 0),
					new IntPoint(doc.ImageSize.Width, doc.ImageSize.Height),
					new IntPoint(0, doc.ImageSize.Height)
				}
			};

			Clipper clipper = new Clipper();

			// subject polygons = selection polygons
			clipper.AddPolygons(subjectPolygons, ClipperLibrary.PolyType.ptSubject); 

			// clip polygons = canvas rectangle polygon
			clipper.AddPolygons(clipPolygons, ClipperLibrary.PolyType.ptClip); 

			List<List<IntPoint>> solution = new List<List<IntPoint>>();
			clipper.Execute(ClipType.ctIntersection, solution);

			return solution;
		}

		private void ConstrainSquare(ref PointD moving, PointD anchor)
		{
			double dx = moving.X - anchor.X;
			double dy = moving.Y - anchor.Y;

			double size = Math.Max(Math.Abs(dx), Math.Abs(dy));

			moving.X = anchor.X + Math.Sign(dx) * size;
			moving.Y = anchor.Y + Math.Sign(dy) * size;
		}

        /// <summary>
        /// Update the selection handles' positions, and redraw them.
        /// </summary>
        private void UpdateHandler ()
		{
			var doc = PintaCore.Workspace.ActiveDocument;

			if (doc != null)
				doc.ToolLayer.Hidden = false;

		    ClearHandles (doc.ToolLayer);
			RefreshHandler();
            DrawHandler (doc.ToolLayer);
		}

        /// <summary>
        /// Erase previously-drawn handles.
        /// </summary>
	    private void ClearHandles (Layer layer)
	    {
            using (var g = new Context (layer.Surface))
		    {
		        foreach (var tool_control in controls)
                    tool_control.Clear (g);
		    }
	    }
	}
}
