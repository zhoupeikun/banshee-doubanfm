AC_DEFUN([BCE_DOUBANFM],
[
	BCE_ARG_DISABLE([DoubanFM], [no])

	if test "x$enable_DoubanFM" = "xyes"; then
		AM_CONDITIONAL(ENABLE_DOUBANFM, true)
	else
		AM_CONDITIONAL(ENABLE_DOUBANFM, false)
	fi
])

