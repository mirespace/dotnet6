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
    echo "$0 [--bootstrap] <tag-from-installer-repo>"
    echo
    echo "Creates a source archive from a tag at https://github.com/dotnet/installer"
    echo ""
    echo "  --bootstrap     build a source tarball usable for bootstrapping .NET"
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

function check_bootstrap_environment {
    if dpkg -l | grep dotnet ; then
        echo "error: dotnet is installed. Not a good idea for bootstrapping."
        exit 1
    fi
    if [ -d /usr/lib/dotnet ] || [ -d /usr/lib64/dotnet ] || [ -d /usr/share/dotnet ] ; then
        echo "error: one of /usr/lib/dotnet /usr/lib64/dotnet or /usr/share/dotnet/ exists. Not a good idea for bootstrapping."
        exit 1
    fi
    if command -v dotnet ; then
        echo "error: dotnet is in $PATH. Not a good idea for bootstrapping."
        exit 1
    fi
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
        --bootstrap)
            check_bootstrap_environment
            build_bootstrap=true
            shift
            ;;
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

if [[ ${build_bootstrap} == true ]]; then
#    unmodified_tarball_name="${unmodified_tarball_name}-${arch}-bootstrap"
#    tarball_name="${tarball_name}-${arch}-bootstrap"
    tarball_suffix=.tar.xz
fi

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

if [[ ${build_bootstrap} == true ]]; then
    if [[ "$(wc -l < packages/archive/archiveArtifacts.txt)" != 1 ]]; then
        echo "error: this is not going to work! update $0 to fix this issue."
        exit 1
    fi

    pushd packages/archive/
    curl -O "$(cat archiveArtifacts.txt)"
    popd

    mkdir foo
    pushd foo

    tar xf ../packages/archive/Private.SourceBuilt.Artifacts.*.tar.gz
    sed -i -E 's|<MicrosoftNETHostModelPackageVersion>6.0.0-rtm.21521.1</|<MicrosoftNETHostModelPackageVersion>6.0.0-rtm.21521.4</|' PackageVersions.props
    sed -i -E 's|<MicrosoftNETHostModelVersion>6.0.0-rtm.21521.1</|<MicrosoftNETHostModelVersion>6.0.0-rtm.21521.4</|' PackageVersions.props
    cat PackageVersions.props

    tar czf ../packages/archive/Private.SourceBuilt.Artifacts.*.tar.gz *

    popd
    rm -rf foo

    ./prep.sh --bootstrap

    mkdir -p fixup-previously-source-built-artifacts
    pushd fixup-previously-source-built-artifacts
    tar xf ../packages/archive/Private.SourceBuilt.Artifacts.*.tar.gz
    find . -iname '*ubuntu*nupkg' -delete
    # We must keep the original file names in the archive, even prepending a ./ leads to issues
    tar -I 'gzip -1' -cf ../packages/archive/Private.SourceBuilt.Artifacts.*.tar.gz *
    popd
    rm -rf fixup-previously-source-built-artifacts
fi

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

if [[ ${build_bootstrap} == true ]]; then
    tar -I 'xz -T 0' -cf "../${tarball_name}${tarball_suffix}" "${tarball_name}"
    find . -type f -iname '*.tar.gz' -delete
    rm -rf .dotnet
else
    find . -type f -iname '*.tar.xz' -delete
    rm -rf .dotnet
    tar -czf "../${tarball_name}${tarball_suffix}" "${tarball_name}"
fi
