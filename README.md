# dotnet6


Packaging .Net 6 on Ubuntu

Here we have only the debian folder for packaging .Net 6 on Ubuntu, due to size limitations. 
This repo is going to be used during the pre-review process. Then, the code will be submitted to 
my forked repo of https://github.com/canonical/dotnet.

It's prepared to recreate v6.0.104.

To recreate tarball and source code for building the package, the steps are the following:

0. Clone the repository and get into it
1. `uscan --download-version 6.0.104`
2. (optional) `uupdate dotnet-6.0.104.tar.xz`
3. (optional) `cp dotnet6-6.0.104.tar.xz ..`
4. (optional) `cd ../dotnet6-6.0.104`
5. `sbuild -d jammy --arch=amd64  --purge-build=successful --debbuildopts='--buildinfo-option=-O' --build-dir=..`


**Some context**

(Please, Note that there is work currently on the MSFT side to have a proper upstream git repo to avoid the following inconveniences).

The source code is a conglomerate of the entire dotnet repo suite (https://github.com/dotnet): from their ~25 different repos, we get a copy of them at a certain commit (src/<dotnet_repo_name>.<hash>), which we get using an MSBuild tool to build the tarball using the release tag (6.0.103, 6.0.104, et al.):
 * Source tarball is 5.5GB.
 * There is no upstream git repo as-is to work from: the way we work and deal with it is by creating the tarball via a script, deploying it locally and keepìng the debian folder in a github repo (cf: https://github.com/canonical/dotnet). We’re working on how to allocate this local snapshot of the upstream code as it exceeds the default GitHub repository size.
 * This makes it impossible to use the series files for patching, as the path from one release to another is changing due to the commit hash.
 * We cannot minimize delta as the source code is always removed and added back, sadly.

  Building time is nearly an hour or more (even through Launchpad). Tarball build is about 30~40 min. We have preliminary versions of the packages here: https://launchpad.net/~cloud-images/+archive/ubuntu/dotnet/+packages
  
  
**Requirements for building**
  
  * Locally:
  For the above timing, I use `DEB_BUILD_OPTIONS="parallel=16"` in front of the sbuild command (my machine has 64 MB RAM and an AMD 9 Ryzen 5000 series processor).
  The space needed is ~50 GB (including the generated .deb files in parent directory. Build log file is ~125MB).
  To install the debs in a VM, I use to create it with 30GB of disk space.
  
  * PPA:
    It's necessary to ask to increase disk space to at least to 10GB.
