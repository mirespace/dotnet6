#!/bin/bash

# Usage:
#   build-dotnet-tarball repo version
#
# Creates a source archive from a branch version at a repo.
# Installer repo is only used for verification of the last tag,
# but target repo a target branch are prompt asked.

# we need to clone the private repo it with an specific branch,
# remove some specific files (the ones included in the Exclude-Files
# section of the debian/copyright file, and pack the tarball)



trap on_exit TERM
trap on_exit EXIT

set -euo pipefail
IFS=$'\n\t'

function print_usage {
    echo "Usage:"
    echo "$0 repo"
    echo ""
    echo "Creates a source archive tarball for dotnet6"
    echo ""
    echo " repo: Address to the repo to be cloned"
    echo ""
#    echo " version (X.X.XXX): dotnet version to be cloned"
#    echo ""
    echo "example: $0 dev.azure.com/dotnet"
}

function on_exit {

    rm -rf dotnet
    rm -rf "${dir_name}"
}


function clean_uscan_download {
   find .. -name "dotnet*${version}*.tar.*" -delete
}


set -x

if [[ "$#" -gt 0 ]]; then
    if [ "${1}" = "-h" ] || [ "${1}" = "--help" ]; then 
       print_usage
       exit 0
    else
	repo="${1}"
	version="${3:-}"
    fi
else        
    echo "Not enought arguments to run properly:"
    print_usage
    exit 1
fi
  

clean_uscan_download

#while : ; do
#    read -p "Repo address for cloning: " repo
#    [ -z "${repo}" ] || break
#done

#while : ; do
#    read -p "dotnet version (X.X.XXX): " version
#    [ -z "${version}" ] || break
#done

#repo="${1}"
#version="${2}"

dir_name="dotnet6-${version}"
tarball_name="dotnet6_${version}.orig"
tarball_suffix=".tar.xz"
tarball="${tarball_name}${tarball_suffix}"


if [ -f "${tarball_name}${tarball_suffix}" ]; then
    echo "error: ${tarball_name}${tarball_suffix} already exists"
    exit 1
fi

git clone $repo --branch=v${version}-SDK


if [ $? -eq 0 ]; then

    pushd dotnet

    # Remove files with funny licenses, crypto implementations and other
    # not-very-useful artifacts to reduce tarball size
    # This list concords with the File-Excluded stanza in the copyright

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

    #Remove vendorized libunwind
    rm -r src/runtime/src/coreclr/pal/src/libunwind

    #CPC-1578 prebuilts not used in build
    rm src/roslyn/src/Compilers/Test/Resources/Core/DiagnosticTests/ErrTestMod01.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/DiagnosticTests/ErrTestMod02.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/ExpressionCompiler/LibraryA.winmd
    rm src/roslyn/src/Compilers/Test/Resources/Core/ExpressionCompiler/LibraryB.winmd
    rm src/roslyn/src/Compilers/Test/Resources/Core/ExpressionCompiler/Windows.Data.winmd
    rm src/roslyn/src/Compilers/Test/Resources/Core/ExpressionCompiler/Windows.Storage.winmd
    rm src/roslyn/src/Compilers/Test/Resources/Core/ExpressionCompiler/Windows.winmd
    rm src/roslyn/src/Compilers/Test/Resources/Core/MetadataTests/Invalid/EmptyModuleTable.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/MetadataTests/NetModule01/ModuleCS00.mod
    rm src/roslyn/src/Compilers/Test/Resources/Core/MetadataTests/NetModule01/ModuleCS01.mod
    rm src/roslyn/src/Compilers/Test/Resources/Core/MetadataTests/NetModule01/ModuleVB01.mod
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/CustomModifiers/Modifiers.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/MultiModule/mod2.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/MultiModule/mod3.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/MultiTargeting/Source1Module.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/MultiTargeting/Source3Module.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/MultiTargeting/Source4Module.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/MultiTargeting/Source5Module.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/MultiTargeting/Source7Module.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/RetargetingCycle/V1/ClassB.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/TypeForwarders/Forwarded.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/V1/MTTestModule1.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/V1/MTTestModule2.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/V2/MTTestModule1.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/V2/MTTestModule3.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/V3/MTTestModule1.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/V3/MTTestModule4.netmodule
    rm 'src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/With Spaces.netmodule'
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/netModule/CrossRefModule1.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/netModule/CrossRefModule2.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/netModule/hash_module.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/netModule/netModule1.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/SymbolsTests/netModule/netModule2.netmodule
    rm src/roslyn/src/Compilers/Test/Resources/Core/WinRt/W1.winmd
    rm src/roslyn/src/Compilers/Test/Resources/Core/WinRt/W2.winmd
    rm src/roslyn/src/Compilers/Test/Resources/Core/WinRt/WB.winmd
    rm src/roslyn/src/Compilers/Test/Resources/Core/WinRt/WB_Version1.winmd
    rm src/roslyn/src/Compilers/Test/Resources/Core/WinRt/WImpl.winmd
    rm src/roslyn/src/Compilers/Test/Resources/Core/WinRt/WinMDPrefixing.winmd
    rm src/roslyn/src/Compilers/Test/Resources/Core/WinRt/Windows.Languages.WinRTTest.winmd
    rm src/roslyn/src/Compilers/Test/Resources/Core/WinRt/Windows.winmd
    rm src/roslyn/src/ExpressionEvaluator/Core/Source/ExpressionCompiler/Resources/WindowsProxy.winmd
    rm src/runtime/src/libraries/System.Reflection.Metadata/tests/Resources/NetModule/ModuleCS00.mod
    rm src/runtime/src/libraries/System.Reflection.Metadata/tests/Resources/NetModule/ModuleCS01.mod
    rm src/runtime/src/libraries/System.Reflection.Metadata/tests/Resources/NetModule/ModuleVB01.mod
    rm src/runtime/src/libraries/System.Reflection.Metadata/tests/Resources/WinRT/Lib.winmd
    rm src/linker/external/cecil/Test/Resources/assemblies/ManagedWinmd.winmd
    rm src/linker/external/cecil/Test/Resources/assemblies/NativeWinmd.winmd
    rm src/linker/external/cecil/Test/Resources/assemblies/moda.netmodule
    rm src/linker/external/cecil/Test/Resources/assemblies/modb.netmodule
    rm src/linker/external/cecil/Test/Resources/assemblies/winrtcomp.winmd

    popd
else
    echo "An error ocurred when clonning $repo --branch=v${version}-SDK"
    exit 1
fi

mv dotnet "${dir_name}"
tar -I 'xz -T 0' -cf "../${tarball}" "${dir_name}"
rm -rf "${dir_name}"
