# dotnet6
Packaging .Net 6 on Ubuntu

Here we have only the debian folder for packaging .Net 6 on Ubuntu, due to size limitations.

It's prepared to recreate v6.0.103-source-build.

To recreate tarball and source code for building the package, the steps are the following:

0. Clone the repository and get into it
1. uscan --download-version 6.0.103
2. uupdate dotnet-6.0.103.tar.xz
3. cp dotnet-6.0.103.tar.xz ..
4. cd ../dotnet6-6.0.103
5. sbuild -d jammy --arch=amd64  --purge-build=successful --debbuildopts='--buildinfo-option=-O' --build-dir=..
