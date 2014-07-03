AC_DEFUN([BCE_ONLINEMETADATAFIXER],
[
	BCE_ARG_DISABLE([OnlineMetadataFixer], [yes])

	if test "x$enable_OnlineMetadataFixer" = "xyes"; then
		AM_CONDITIONAL(ENABLE_ONLINEMETADATAFIXER, true)
		PKG_CHECK_MODULES(BANSHEE_FIXUP, banshee-fixup >= 2.9.2)
		AC_SUBST(BANSHEE_FIXUP_LIBS)
	else
		AM_CONDITIONAL(ENABLE_ONLINEMETADATAFIXER, false)
	fi
])

