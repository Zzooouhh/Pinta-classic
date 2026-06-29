# Pinta-classic - patchset for Pinta 1.7.1

This is an independent fork of Pinta 1.7.1 intended to address certain long-standing usability issues and missing UI features in this version of the application, while preserving the classic GTK2 interface and being simple to implement and maintain over the base version. Rather than file bug reports and wait for upstream changes, I've decided to implement these directly into the codebase on my own with the assistance of LLM tools. All of the patches that came out of this process were individually tested by myself to ensure their intended functionality.

This is not intended as a replacement for the current upstream Pinta project, rather it is an alternative for users who prefer the GTK2-based interface and would prefer a version of Pinta with a more Paint.NET styled workflow.

Reasons for basing this project on Pinta 1.7.1 include:

1. Some Linux distributions and FreeBSD still include Pinta 1.7.1 as the latest packaged version. This fork originally began as (and still remains) a drop-in replacement for the FreeBSD port.
2. The 1.7.1 codebase still contained numerous bugs and usability issues. Pinta Classic addresses many of these and includes workflow improvements inspired by Paint.NET.
3. I prefer the classic GTK2 interface and a keyboard-and-mouse-focused use case aimed for desktop/laptop users.

See `CHANGELOG.md` for a complete list of changes.


## Controls

Most hotkeys and mnemonics should be recognizable by popups, menu displays or other visual indicators. Here is a list of other controls which are not elaborated upon in Pinta-classic itself:

### Zoom/Pan
| Key | Action |
| --- | --- |
| Mousewheel | scroll canvas |
| Shift+Mousewheel | scroll canvas (horizontal only) |
| Ctrl+Mousewheel | zoom in/out |
| +,- | zoom in/out |
| Hold middle mouse | pan |

### Pencil/Brush/Eraser/Recolor
| Key | Action |
| --- | --- |
| Alt+draw | quick color picker |
| Shift+draw | straight line snapped to a 15 degree angle |
| Ctrl+Shift+draw | straight line not snapped to an angle (full 360 degrees) |
| [] | change brush size (by 1) |
| Shift+[] | change brush size (by 5) |
| Ctrl+Shift+A | toggle Antialiasing on/off |

### Fill/Magic Wand/Recolor
| Key | Action |
| --- | --- |
| Ctrl+[] | change tolerance (by 1) |
| Ctrl+Shift+[] | change tolerance (by 10) |
| Ctrl+Shift+F (for Fill, Magic Wand) | toggle Flood mode global/contiguous |
| Hold Shift+draw (while using Fill, Magic Wand) | apply the unselected flood mode (e.g. if Global is selected, Contiguous will be used) |

### Selection
| Key | Action |
| --- | --- |
| Esc, Shift+D, Enter | any of these will deselect selection |
| Arrow keys | move selection (by 1) |
| Ctrl+Arrow keys | move selection (by 10) |

### Shapes
| Key | Action |
| --- | --- |
| PageUp/PageDown | cycle between control points of current shape |
| Shift+PageUp/PageDown | cycle between shapes (first control point will be selected) |

### Tabs
| Key | Action |
| --- | --- |
| Ctrl+Tab, Ctrl+PageDown | go to next tab |
| Ctrl+Shift+Tab, Ctrl+PageUp | go to previous tab |
| Ctrl+Shift+PageUp/PageDown | move current tab to next/previous index |
| Alt+number | jump to tab with specified index |


The remainder of this README describes the original Pinta 1.7.1 release and much of it remains applicable to Pinta-classic.


# Pinta - [Simple Gtk# Paint Program](http://pinta-project.com/)

[![Build Status](https://github.com/PintaProject/Pinta/workflows/Build/badge.svg)](https://github.com/PintaProject/Pinta/actions)

Copyright (C) 2010 Jonathan Pobst <monkey AT jpobst DOT com>

Pinta is a Gtk# clone of [Paint.Net 3.0](http://www.getpaint.net/)

Original Pinta code is licensed under the MIT License:
See `license-mit.txt` for the MIT License

Code from Paint.Net 3.36 is used under the MIT License and retains the
original headers on source files.

See `license-pdn.txt` for Paint.Net's original license.


## Icons are from:

- [Paint.Net 3.0](http://www.getpaint.net/)
Used under [MIT License](http://www.opensource.org/licenses/mit-license.php)

- [Silk icon set](http://www.famfamfam.com/lab/icons/silk/)
Used under [Creative Commons Attribution 3.0 License](http://creativecommons.org/licenses/by/3.0/)

- [Fugue icon set](http://pinvoke.com/)
Used under [Creative Commons Attribution 3.0 License](http://creativecommons.org/licenses/by/3.0/)

## Getting help/contributing:

- You can get technical help on the [Pinta Google Group](https://groups.google.com/group/pinta-project)
- You can report bugs/issues on [Launchpad bug tracker](https://bugs.launchpad.net/pinta/+filebug)
- You can make suggestions at [Communiroo](https://communiroo.com/pintaproject/pinta/suggestions)
- You can help translate Pinta to your native language on [Launchpad translations](https://translations.launchpad.net/pinta)
- You can fork the project on [Github](https://github.com/PintaProject/Pinta)
- You can get help in #pinta on irc.gnome.org.
- For details on patching, take a look at `patch-guidelines.md` in the repo.


## Windows Build and Installation Instructions:

Be sure to install [Gtk# for Windows](https://xamarin.azureedge.net/GTKforWindows/Windows/gtk-sharp-2.12.45.msi) when building in Visual Studio.

## Linux Build and Installation Instructions:

Building Pinta requires the following software (instructions are for Ubuntu 20.04, but should be similar for other distributions):

`sudo apt install make automake autoconf mono-devel gtk-sharp2 intltool`

Pinta only supports version 2.8 or higher of Mono.

To build Pinta, run:

`./autogen.sh`

`make`

`sudo make install`

or if building from a tarball, run:

`./configure`

`make`

`sudo make install`

To use different installation directory than the default (/usr/local), run this instead:

`./autogen.sh --prefix=<install directory>`


To uninstall Pinta, run:

`sudo make uninstall`

To clean all files created during the build process, run:

`make cleanall`

**Note** This will require you to rerun `autogen.sh` in order to run more `make` commands.

For a list of more make commands, run:

`make help`
