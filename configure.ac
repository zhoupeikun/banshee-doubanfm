AC_PREREQ(2.52)
AC_INIT([banshee-extension-doubanfm], 0.1.2, [http://code.google.com/p/banshee-doubanfm])

AM_INIT_AUTOMAKE([1.9 dist-bzip2 tar-ustar dist-zip foreign])
AM_MAINTAINER_MODE
AM_CONFIG_HEADER([config.h])
AC_SUBST([ACLOCAL_AMFLAGS], ["-I build/m4/shamrock -I build/m4/shave -I build/m4/extensions \${ACLOCAL_FLAGS}"])

IT_PROG_INTLTOOL([0.35.0])
AC_PROG_LIBTOOL
AC_PROG_INSTALL

dnl How to configure for distcheck
DISTCHECK_CONFIGURE_FLAGS="--enable-release --enable-tests"
AC_SUBST(DISTCHECK_CONFIGURE_FLAGS)

dnl Expansions
SHAMROCK_EXPAND_LIBDIR
SHAMROCK_EXPAND_BINDIR
SHAMROCK_EXPAND_DATADIR

dnl User help
SHAMROCK_CHECK_GNOME_DOC_UTILS(0.17.3)

dnl Mono and gmcs
SHAMROCK_CHECK_MONO_MODULE(1.9.1)
SHAMROCK_FIND_MONO_2_0_COMPILER
SHAMROCK_FIND_MONO_RUNTIME

dnl Banshee deps
PKG_CHECK_MODULES(BANSHEE, banshee-thickclient >= 1.9.6)
AC_SUBST(BANSHEE_LIBS)

dnl Banshee Extensions Path
BCE_FIND_BANSHEE

dnl For native/glue libraries
AC_ISC_POSIX
AC_PROG_CC
AC_PROG_CXX
AC_HEADER_STDC

dnl GNOME (optional)
AC_ARG_ENABLE(gnome, 
	AC_HELP_STRING([--disable-gnome], 
		[Disable GNOME support]), 
	, enable_gnome="yes")
if test "x$enable_gnome" = "xyes"; then
	AC_PATH_PROG(GCONFTOOL, gconftool-2, no)
	AM_GCONF_SOURCE_2
	AM_CONDITIONAL(ENABLE_GNOME, true)
else
	AM_CONDITIONAL(GCONF_SCHEMAS_INSTALL, false)
	AM_CONDITIONAL(ENABLE_GNOME, false)
fi

dnl i18n
SHAMROCK_CONFIGURE_I18N($PACKAGE)

dnl Release
AC_ARG_ENABLE(release,
	AC_HELP_STRING([--enable-release],
		[Use release configuration]),
	enable_release=yes, enable_release=`(test ! -d .svn && test ! -d .git) && echo "yes" || echo "no"`)
AM_CONDITIONAL(ENABLE_RELEASE, test "x$enable_release" = "xyes")
ENABLE_RELEASE=$enable_release
AC_SUBST(ENABLE_RELEASE)
if test "x$enable_release" = "xyes"; then
	GMCS_FLAGS="-define:RELEASE"
else
	GMCS_FLAGS="-warnaserror"
fi
AC_SUBST(GMCS_FLAGS)

dnl Unit Tests (optional)
SHAMROCK_CHECK_UNIT_TESTS

dnl Extensions
BCE_DOUBANFM

AC_ARG_WITH(vendor-build-id,
	AC_HELP_STRING([--with-vendor-build-id=<vendor-build-id>],
		[Set a vendor build ID, recommended for packagers]),
	[vendor_build_id="$withval"], [
		test -d .git \
			&& vendor_build_id="git-checkout" \
			|| vendor_build_id="source-tarball"
	])
BUILD_VENDOR_ID="$vendor_build_id"
BUILD_HOST_OS="$host_os"
BUILD_HOST_CPU="$host_cpu"
BUILD_TIME=`date +"%F %T %Z"`
AC_SUBST(BUILD_VENDOR_ID)
AC_SUBST(BUILD_HOST_OS)
AC_SUBST(BUILD_HOST_CPU)
AC_SUBST(BUILD_TIME)

SHAVE_INIT([build/m4/shave], [enable])

dnl generated files
AC_OUTPUT([
Makefile

build/Makefile
build/m4/Makefile
build/m4/shave/shave
build/m4/shave/shave-libtool

po/Makefile.in
src/AssemblyInfo.cs
src/Makefile
src/DoubanFM/Makefile
])

cat <<EOF

${PACKAGE}-${VERSION}

  Build Environment
    Install Prefix:      ${prefix}
    Datadir:             ${expanded_datadir}
    Libdir:              ${expanded_libdir}
    Extensions libdir:   ${expanded_extensionsdir}

    C Compiler:          ${CC}
    Mono C# Compiler:    ${MCS} ${GMCS_FLAGS}
    Mono Runtime:        ${MONO}

  Build/Development:
    Release Build:       ${enable_release}
    Vendor Build ID:     ${vendor_build_id}
    Unit Tests:          ${enable_tests} (requires nunit >= ${NUNIT_REQUIRED}, --enable-tests)
    User Help:           ${enable_user_help} (requires gnome-doc-utils >= 0.17.3)

  Extensions:
    DoubanFM:            yes

EOF
