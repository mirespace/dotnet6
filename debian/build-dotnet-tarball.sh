#!/bin/bash

# Usage:
#   build-dotnet-tarball [--bootstrap] <tag-from-installer-repo>
#
# Creates a source archive from a tag (or commit) at github.com/dotnet/installer

# installer is a little strange, we need to clone it, check out the
# tag, build it and then create a tarball from the archive directory
# it creates. Also, it is likely that the source archive is only
# buildable on the OS it was initially created in.

# This script has modifications on the work done
# by Omair Majid <omajid@redhat.com>. Thanks to him!


trap on_exit TERM
trap on_exit EXIT

set -euo pipefail
IFS=$'\n\t'

function print_usage {
    echo "Usage:"
    echo "$0 <tag-from-installer-repo>"
    echo
    echo "Creates a source archive from a tag at https://github.com/dotnet/installer"
}

function on_exit {

    local tarballname
    local tempdir

    clean_dotnet_cache

    tarballname=${tarball_name:-}
    tempdir=${temp_dir:-}

    folders_to_clean=("foo" "${tarballname}" "${tempdir}" \
                      "fixup-previously-source-built-artifacts" ".dotnet")

    for folder in "${folders_to_clean[@]}"; do
        if [ -d "$folder" ]; then
            rm -rf "$folder"
        fi
    done

    find . -type f -iname '*.tar.gz' -delete

}

function clean_dotnet_cache {

    folders_cached=("/tmp/NuGet" "/tmp/NuGetScratch" "/tmp/.NETCore*" "/tmp/.NETStandard*" "/tmp/.dotnet" \
                    "/tmp/dotnet.*" "/tmp/clr-debug-pipe*" "/tmp/Razor-Server" "/tmp/CoreFxPipe*" "/tmp/VBCSCompiler" \
                    "/tmp/.NETFramework*")

    for folder in "${folders_cached[@]}"; do
        if [ -d "$folder" ]; then
            rm -rf "$folder"
        fi
    done

}

function clean_uscan_download {
   find .. -name "dotnet*${tag}*.tar.*" -delete
}

function runtime_id {

    source /etc/os-release

    #echo "${ID}.${VERSION_ID}-${arch}"
    [ -n "${VERSION_ID}" ] && echo "${ID}.${VERSION_ID}-${arch}" || echo "${ID}-${arch}"
}

build_bootstrap=false

declare -A archmap
archmap=(
    ["aarch64"]="arm64"
    ["amd64"]="x64"
    ["armv8l"]="arm"
    ["i686"]="x86"
    ["i386"]="x86"
    ["x86_64"]="x64"
)

arch=${archmap["$(uname -m)"]}


positional_args=()
while [[ "$#" -gt 0 ]]; do
    arg="${1}"
    case "${arg}" in
        -h|--help)
            print_usage
            exit 0
            ;;
        --upstream-version)
            positional_args+=("$2")
            shift
            shift
            ;;
    esac
done


tag=${positional_args[0]:-}
if [[ -z ${tag} ]]; then
    echo "error: missing tag to build"
    exit 1
fi

set -x


dir_name="dotnet6_${tag}"
unmodified_tarball_name="${dir_name}.orig_not_clean"
tarball_name="${dir_name}.orig"
tarball_suffix=.tar.gz

clean_uscan_download

if [ -f "${tarball_name}${tarball_suffix}" ]; then
    #rm "${tarball_name}${tarball_suffix}"
    echo "error: ${tarball_name}${tarball_suffix} already exists"
    exit 1
fi

if [ ! -f "${unmodified_tarball_name}.tar.gz" ]; then
    temp_dir=$(mktemp -d -p "$(pwd)")
    pushd "${temp_dir}"
    git clone https://github.com/dotnet/installer
    pushd installer
    git checkout "v${tag}"
    git submodule update --init --recursive
    clean_dotnet_cache
    mkdir -p "../${unmodified_tarball_name}"
    ./build.sh /p:ArcadeBuildTarball=true /p:TarballDir="$(readlink -f ../"${unmodified_tarball_name}")" /p:CleanWhileBuilding=true
    popd

    popd

    tar cf "${unmodified_tarball_name}.tar.gz" -C "${temp_dir}" "${unmodified_tarball_name}"

    rm -rf "${temp_dir}"
fi

rm -rf "${tarball_name}"
tar xf "${unmodified_tarball_name}.tar.gz"
mv "${unmodified_tarball_name}" "${tarball_name}"

pushd "${tarball_name}"

# Remove files with funny licenses, crypto implementations and other
# not-very-useful artifacts to reduce tarball size

# Binaries for gradle
rm -r src/aspnetcore/src/SignalR/clients/java/signalr/gradle*

# Unnecessary crypto implementation: IDEA
rm -r src/runtime/src/tests/JIT/Performance/CodeQuality/Bytemark/

# https://github.com/dotnet/aspnetcore/issues/34785
find src/aspnetcore/src -type d -name samples -print0 | xargs -0 rm -r

# https://github.com/NuGet/Home/issues/11094
rm -r src/nuget-client/test/EndToEnd

# https://github.com/Humanizr/sample-aspnetmvc/issues/1
rm -r src/source-build/src/humanizer/samples/

#Non-free and unnecesary help file for 7-zip
rm src/source-build/src/newtonsoft-json901/Tools/7-zip/7-zip.chm

#Checked that are not needed in the build: this only removes under roslyn:
#src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/V?/*.dll
find . -iname "*.dll" -exec rm -rf {} +

popd

find . -type f -iname '*.tar.xz' -delete
rm -rf .dotnet
tar -czf "../${tarball_name}${tarball_suffix}" "${tarball_name}"

