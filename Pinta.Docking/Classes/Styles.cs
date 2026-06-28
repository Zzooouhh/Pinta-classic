// 
// Styles.cs
//  
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
// 
// Copyright (c) 2012 Xamarin Inc
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
using MonoDevelop.Components;

namespace Pinta.Docking
{
	public static class Styles
	{
		public static readonly Cairo.Color BaseBackgroundColor;
		public static readonly Cairo.Color BaseForegroundColor;
		public static readonly bool IsDarkTheme;

		static Styles ()
		{
			var widget = new Gtk.Window (Gtk.WindowType.Popup);

            // Force GTK to create the GdkWindow/style
            widget.Realize ();

			var style = widget.Style;

			BaseBackgroundColor =
				style.Background (Gtk.StateType.Normal).ToCairoColor ();

			BaseForegroundColor =
				style.Foreground (Gtk.StateType.Normal).ToCairoColor ();

			IsDarkTheme = 
				(BaseBackgroundColor.R +
				 BaseBackgroundColor.G +
				 BaseBackgroundColor.B) / 3.0 < 0.5;

            TabBarActiveTextColor = IsDarkTheme ? new Cairo.Color (1, 1, 1) : new Cairo.Color (0, 0, 0);
            PadBackground = IsDarkTheme ? new Gdk.Color (15, 15, 15) : new Gdk.Color (240, 240, 240);

			widget.Destroy ();
		}

		// General

		public static readonly Gdk.Color ThinSplitterColor = Darken (BaseBackgroundColor, 0.18).ToGdkColor ();

		// Document tab bar
		public static readonly Cairo.Color TabBarBackgroundColor = BaseBackgroundColor;
		public static Cairo.Color TabBarActiveTextColor { get; private set; }

		public static readonly Cairo.Color TabBarActiveGradientStartColor = IsDarkTheme ? Lighten (TabBarBackgroundColor, 0.04) : Darken (TabBarBackgroundColor, 0.03);
		public static readonly Cairo.Color TabBarActiveGradientEndColor = TabBarBackgroundColor;
		public static readonly Cairo.Color TabBarGradientStartColor = IsDarkTheme ? Lighten (TabBarBackgroundColor, 0.02) : Lighten (TabBarBackgroundColor, 0.01);
		public static readonly Cairo.Color TabBarGradientEndColor = TabBarBackgroundColor;
		public static readonly Cairo.Color TabBarGradientShadowColor = IsDarkTheme ? Darken (TabBarBackgroundColor, 0.12) : Darken (TabBarBackgroundColor, 0.08);
		public static readonly Cairo.Color TabBarHoverActiveTextColor = BaseForegroundColor;
		public static readonly Cairo.Color TabBarInactiveTextColor = Blend (BaseForegroundColor, BaseBackgroundColor, IsDarkTheme ? 0.25 : 0.4);
		public static readonly Cairo.Color TabBarHoverInactiveTextColor = BaseForegroundColor;

		public static readonly Cairo.Color BreadcrumbGradientStartColor = TabBarBackgroundColor;
		public static readonly Cairo.Color BreadcrumbInactiveGradientStartColor = TabBarBackgroundColor;
		public static readonly Cairo.Color BreadcrumbBackgroundColor = IsDarkTheme ? Darken (BaseBackgroundColor, 0.04) : Lighten (BaseBackgroundColor, 0.01);
		public static readonly Cairo.Color BreadcrumbGradientEndColor = BreadcrumbBackgroundColor;
		public static readonly Cairo.Color BreadcrumbBorderColor = Darken (BreadcrumbBackgroundColor, 0.15);
		public static readonly Cairo.Color BreadcrumbInnerBorderColor = WithAlpha (BaseForegroundColor, IsDarkTheme ? 0.04 : 0.08);
		public static readonly Gdk.Color BreadcrumbTextColor = BaseForegroundColor.ToGdkColor ();
		public static readonly Cairo.Color BreadcrumbButtonBorderColor = Darken (BreadcrumbBackgroundColor, 0.1);
		public static readonly Cairo.Color BreadcrumbButtonFillColor = WithAlpha (BaseForegroundColor, 0.03);
		public static readonly Cairo.Color BreadcrumbBottomBorderColor = Darken (BreadcrumbBackgroundColor, 0.18);
		public static readonly bool BreadcrumbInvertedIcons = false;
		public static readonly bool BreadcrumbGreyscaleIcons = false;

		// Dock pads
		
		public static readonly Cairo.Color DockTabBarGradientTop = IsDarkTheme ? Lighten (BaseBackgroundColor, 0.02) : Lighten (BaseBackgroundColor, 0.01);
		public static readonly Cairo.Color DockTabBarGradientStart = BaseBackgroundColor;
		public static readonly Cairo.Color DockTabBarGradientEnd = IsDarkTheme ? Darken (BaseBackgroundColor, 0.03) : Darken (BaseBackgroundColor, 0.02);
		public static readonly Cairo.Color DockTabBarShadowGradientStart = new Cairo.Color (0, 0, 0, IsDarkTheme ? 0.22 : 0.12);
		public static readonly Cairo.Color DockTabBarShadowGradientEnd = new Cairo.Color (0, 0, 0, 0);

		public static Gdk.Color PadBackground { get; private set; }
		public static readonly Gdk.Color InactivePadBackground = IsDarkTheme ? Darken (BaseBackgroundColor, 0.03).ToGdkColor () : Darken (BaseBackgroundColor, 0.02).ToGdkColor ();
		public static readonly Gdk.Color PadLabelColor = BaseForegroundColor.ToGdkColor ();
		public static readonly Gdk.Color DockFrameBackground = Darken (BaseBackgroundColor, 0.1).ToGdkColor ();
		public static readonly Gdk.Color DockSeparatorColor = ThinSplitterColor;

		public static readonly Gdk.Color BrowserPadBackground = PadBackground;
		public static readonly Gdk.Color InactiveBrowserPadBackground = InactivePadBackground;

		public static readonly Cairo.Color DockBarBackground1 = BaseBackgroundColor;
		public static readonly Cairo.Color DockBarBackground2 = IsDarkTheme ? Darken (BaseBackgroundColor, 0.02) : Lighten (BaseBackgroundColor, 0.01);
		public static readonly Cairo.Color DockBarSeparatorColorDark = new Cairo.Color (0, 0, 0, IsDarkTheme ? 0.35 : 0.18);
		public static readonly Cairo.Color DockBarSeparatorColorLight = new Cairo.Color (1, 1, 1, IsDarkTheme ? 0.06 : 0.22);
		public static readonly Cairo.Color DockBarPrelightColor = WithAlpha (BaseForegroundColor, IsDarkTheme ? 0.05 : 0.08);

		// Status area
		public static readonly Cairo.Color WidgetBorderColor = Darken (BaseBackgroundColor, 0.18);

		public static readonly Cairo.Color StatusBarBorderColor = Darken (BaseBackgroundColor, 0.12);

		public static readonly Cairo.Color StatusBarFill1Color = BaseBackgroundColor;
		public static readonly Cairo.Color StatusBarFill2Color = IsDarkTheme ? Darken (BaseBackgroundColor, 0.01) : Lighten (BaseBackgroundColor, 0.01);
		public static readonly Cairo.Color StatusBarFill3Color = IsDarkTheme ? Darken (BaseBackgroundColor, 0.02) : Lighten (BaseBackgroundColor, 0.02);
		public static readonly Cairo.Color StatusBarFill4Color = IsDarkTheme ? Darken (BaseBackgroundColor, 0.03) : Lighten (BaseBackgroundColor, 0.03);

		public static readonly Cairo.Color StatusBarErrorColor = CairoExtensions.ParseColor ("FF6363");

		public static readonly Cairo.Color StatusBarInnerColor = new Cairo.Color (0,0,0, 0.08);
		public static readonly Cairo.Color StatusBarShadowColor1 = new Cairo.Color (0,0,0, 0.06);
		public static readonly Cairo.Color StatusBarShadowColor2 = new Cairo.Color (0,0,0, 0.02);
		public static readonly Cairo.Color StatusBarTextColor = BaseForegroundColor;
		public static readonly Cairo.Color StatusBarProgressBackgroundColor = new Cairo.Color (0, 0, 0, 0.1);
		public static readonly Cairo.Color StatusBarProgressOutlineColor = new Cairo.Color (0, 0, 0, 0.1);

		public static readonly Pango.FontDescription StatusFont = Pango.FontDescription.FromString ("Normal");

		public static int StatusFontPixelHeight { get { return (int)(11 * PixelScale); } }
		public static int ProgressBarHeight { get { return (int)(18 * PixelScale); } }
		public static int ProgressBarInnerPadding { get { return (int)(4 * PixelScale); } }
		public static int ProgressBarOuterPadding { get { return (int)(4 * PixelScale); } }

		static readonly double PixelScale = GtkWorkarounds.GetPixelScale ();

		// Toolbar

		public static readonly Cairo.Color ToolbarBottomBorderColor = new Cairo.Color (0.5, 0.5, 0.5);
		public static readonly Cairo.Color ToolbarBottomGlowColor = new Cairo.Color (1, 1, 1, 0.2);

		// Code Completion

		public static readonly int TooltipInfoSpacing = 1;

		// Popover Windows

		public static class PopoverWindow
		{
			public static readonly int PagerTriangleSize = 6;
			public static readonly int PagerHeight = 16;

			public static readonly Cairo.Color DefaultBackgroundColor = CairoExtensions.ParseColor ("fff3cf");
			public static readonly Cairo.Color ErrorBackgroundColor = CairoExtensions.ParseColor ("E27267");
			public static readonly Cairo.Color WarningBackgroundColor = CairoExtensions.ParseColor ("efd46c");
			public static readonly Cairo.Color InformationBackgroundColor = CairoExtensions.ParseColor ("709DC9");

			public static readonly Cairo.Color DefaultBorderColor = CairoExtensions.ParseColor ("ffeeba");
			public static readonly Cairo.Color ErrorBorderColor = CairoExtensions.ParseColor ("c97968");
			public static readonly Cairo.Color WarningBorderColor = CairoExtensions.ParseColor ("e8c12c");
			public static readonly Cairo.Color InformationBorderColor = CairoExtensions.ParseColor ("6688bc");

			public static readonly Cairo.Color DefaultTextColor = CairoExtensions.ParseColor ("665a36");
			public static readonly Cairo.Color ErrorTextColor = CairoExtensions.ParseColor ("ffffff");
			public static readonly Cairo.Color WarningTextColor = CairoExtensions.ParseColor ("563b00");
			public static readonly Cairo.Color InformationTextColor = CairoExtensions.ParseColor ("ffffff");

			public static class ParamaterWindows
			{
				public static readonly Cairo.Color GradientStartColor = CairoExtensions.ParseColor ("fffee6");
				public static readonly Cairo.Color GradientEndColor = CairoExtensions.ParseColor ("fffcd1");
			}
		}

		// Helper methods

		internal static Cairo.Color Shift (Cairo.Color color, double factor)
		{
			return new Cairo.Color (color.R * factor, color.G * factor, color.B * factor, color.A);
		}

		internal static Cairo.Color WithAlpha (Cairo.Color c, double alpha)
		{
			return new Cairo.Color (c.R, c.G, c.B, alpha);
		}

		internal static Cairo.Color Blend (Cairo.Color color, Cairo.Color targetColor, double factor)
		{
			return new Cairo.Color (color.R + ((targetColor.R - color.R) * factor),
			                        color.G + ((targetColor.G - color.G) * factor),
			                        color.B + ((targetColor.B - color.B) * factor),
			                        color.A
			                        );
		}

		internal static Cairo.Color MidColor (double factor)
		{
			return Blend (BaseBackgroundColor, BaseForegroundColor, factor);
		}

		internal static Cairo.Color ReduceLight (Cairo.Color color, double factor)
		{
			var c = color.ToXwtColor ();
			c.Light *= factor;
			return c.ToCairoColor ();
		}

		internal static Cairo.Color IncreaseLight (Cairo.Color color, double factor)
		{
			var c = color.ToXwtColor ();
			c.Light += (1 - c.Light) * factor;
			return c.ToCairoColor ();
		}

		internal static Gdk.Color ReduceLight (Gdk.Color color, double factor)
		{
			return ReduceLight (color.ToCairoColor (), factor).ToGdkColor ();
		}

		internal static Gdk.Color IncreaseLight (Gdk.Color color, double factor)
		{
			return IncreaseLight (color.ToCairoColor (), factor).ToGdkColor ();
		}
        
        internal static Cairo.Color Lighten (Cairo.Color color, double amount)
        {
            return IncreaseLight (color, amount);
        }

        internal static Cairo.Color Darken (Cairo.Color color, double amount)
        {
            return ReduceLight (color, 1.0 - amount);
        }
	}
}

