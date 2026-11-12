# PowerToys overlay port for spdlog.
#
# Pinned to the same git commit that the deleted deps/spdlog submodule pointed
# at, so this is a 1:1 submodule->vcpkg migration with no version change
# (per the maintainer guidance: convert one submodule at a time, atomic
# commit, don't also bump the version).
#
# A single hunk patch works around MSVC 14.51 STL4043 (removal of
# stdext::checked_array_iterator) in spdlog's bundled fmt 7. Drop this overlay
# (and switch to upstream vcpkg's spdlog port) once PowerToys bumps spdlog
# past v1.14, which ships fmt 10.2 and removes the affected code path.

vcpkg_from_github(
    OUT_SOURCE_PATH SOURCE_PATH
    REPO gabime/spdlog
    REF 616866fcf40340ea25a8f218369bad810ef58e72
    SHA512 2076c527c7768627e6856b2f7ef663b185fd6251894cffd9299203d00f3d2de5696461060442dd72b96c9d3f0fd27f7f63ad2edfdf295e9b06c5fac6d6212faf
    HEAD_REF v1.x
    PATCHES
        msvc-14.51-stdext-checked-array-iterator.patch
)

vcpkg_cmake_configure(
    SOURCE_PATH "${SOURCE_PATH}"
    OPTIONS
        -DSPDLOG_BUILD_EXAMPLE=OFF
        -DSPDLOG_BUILD_TESTS=OFF
        -DSPDLOG_BUILD_BENCH=OFF
        -DSPDLOG_FMT_EXTERNAL=OFF
        -DSPDLOG_WCHAR_SUPPORT=ON
        -DSPDLOG_WCHAR_FILENAMES=ON
        -DSPDLOG_NO_EXCEPTIONS=OFF
        -DSPDLOG_BUILD_SHARED=OFF
)

vcpkg_cmake_install()
vcpkg_cmake_config_fixup(PACKAGE_NAME spdlog CONFIG_PATH lib/cmake/spdlog)
vcpkg_fixup_pkgconfig()
vcpkg_copy_pdbs()

file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/include")

vcpkg_install_copyright(FILE_LIST "${SOURCE_PATH}/LICENSE")
