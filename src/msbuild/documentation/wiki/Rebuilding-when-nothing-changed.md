# How to investigate rebuilding when nothing has changed

There is a class of problems with build where when you build twice, it still rebuilds fully the second time even though nothing has changed. This is called build incrementality issues. They can happen in MSBuild or in Visual Studio (in which case the VS project system's up-to-date-check decides to rebuild the project).

There are multiple tools to investigate and fix broken incrementality. Start with the blog posts below.

 * [https://blogs.msdn.microsoft.com/kirillosenkov/2014/08/04/how-to-investigate-rebuilding-in-visual-studio-when-nothing-has-changed/](https://blogs.msdn.microsoft.com/kirillosenkov/2014/08/04/how-to-investigate-rebuilding-in-visual-studio-when-nothing-has-changed/)
 * [https://blogs.msdn.microsoft.com/kirillosenkov/2015/05/12/msbuild-unnecessary-rebuilds-because-of-generated-assemblyattributes-cs/](https://blogs.msdn.microsoft.com/kirillosenkov/2015/05/12/msbuild-unnecessary-rebuilds-because-of-generated-assemblyattributes-cs/)
 * [http://www.andreas-reiff.de/2012/02/when-visual-studio-keeps-rebuilding-projects-that-have-not-changed/](http://www.andreas-reiff.de/2012/02/when-visual-studio-keeps-rebuilding-projects-that-have-not-changed/)
 * [MSDN: How to build incrementally](https://msdn.microsoft.com/en-us/library/ms171483.aspx)
 * [https://docs.microsoft.com/en-us/visualstudio/msbuild/incremental-builds?view=vs-2019](https://docs.microsoft.com/en-us/visualstudio/msbuild/incremental-builds?view=vs-2019)

Strings to search for in the build logs:
 * `Building target "CoreCompile" completely`
 * `is newer than output`
 * `out-of-date`
 * `missing`

Consider using http://msbuildlog.com to help with searching through the build log.