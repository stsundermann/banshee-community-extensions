AC_DEFUN([BCE_CLUTTERFLOW],
[
	BCE_ARG_DISABLE([ClutterFlow], [try])

	BCE_CHECK_EXTENSION_DEP([ClutterFlow], [CLUTTER_SHARP],
		[clutter-sharp clutter-gtk-sharp],
		[clutter-sharp was not found. Please install clutter-sharp or disable the ClutterFlow extension by passing --disable-clutterflow])

	BCE_CHECK_EXTENSION_DEP([ClutterFlow], [BANSHEE_NOWPLAYING],
		[banshee-1-nowplaying >= 1.5.3],
		[The Banshee NowPlaying extension was not found. Please install it or disable the ClutterFlow extension by passing --disable-clutterflow])

	if test "x$enable_ClutterFlow" = "xtry" \
		&& test "x$have_CLUTTER_SHARP" = "xyes" \
		&& test "x$have_BANSHEE_NOWPLAYING" = "xyes"; then
		enable_ClutterFlow=yes
	fi

	if test "x$enable_ClutterFlow" = "xyes"; then
		AM_CONDITIONAL(ENABLE_CLUTTERFLOW, true)
	else
		enable_ClutterFlow=no
		AM_CONDITIONAL(ENABLE_CLUTTERFLOW, false)
	fi
])
