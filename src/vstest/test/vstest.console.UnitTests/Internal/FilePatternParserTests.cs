﻿// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text.RegularExpressions;

namespace vstest.console.UnitTests.Internal
{
    using Microsoft.Extensions.FileSystemGlobbing;
    using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
    using Microsoft.VisualStudio.TestPlatform.CommandLine;
    using Microsoft.VisualStudio.TestPlatform.Utilities.Helpers.Interfaces;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using System.Collections.Generic;
    using vstest.console.Internal;

    [TestClass]
    public class FilePatternParserTests
    {
        private FilePatternParser filePatternParser;
        private Mock<Matcher> mockMatcherHelper;
        private Mock<IFileHelper> mockFileHelper;

        [TestInitialize]
        public void TestInit()
        {
            this.mockMatcherHelper = new Mock<Matcher>();
            this.mockFileHelper = new Mock<IFileHelper>();
            this.filePatternParser = new FilePatternParser(this.mockMatcherHelper.Object, this.mockFileHelper.Object);
        }

        [TestMethod]
        public void FilePatternParserShouldCorrectlySplitPatternAndDirectory()
        {
            var patternMatchingResult = new PatternMatchingResult(new List<FilePatternMatch>());
            this.mockMatcherHelper.Setup(x => x.Execute(It.IsAny<DirectoryInfoWrapper>())).Returns(patternMatchingResult);
            this.filePatternParser.GetMatchingFiles(TranslatePath(@"C:\Users\vanidhi\Desktop\a\c\*bc.dll"));

            // Assert
            this.mockMatcherHelper.Verify(x => x.AddInclude(TranslatePath(@"*bc.dll")));
            this.mockMatcherHelper.Verify(x => x.Execute(It.Is<DirectoryInfoWrapper>(y => y.FullName.Equals(TranslatePath(@"C:\Users\vanidhi\Desktop\a\c")))));
        }

        [TestMethod]
        public void FilePatternParserShouldCorrectlySplitWithArbitraryDirectoryDepth()
        {
            var patternMatchingResult = new PatternMatchingResult(new List<FilePatternMatch>());
            this.mockMatcherHelper.Setup(x => x.Execute(It.IsAny<DirectoryInfoWrapper>())).Returns(patternMatchingResult);
            this.filePatternParser.GetMatchingFiles(TranslatePath(@"C:\Users\vanidhi\**\c\*bc.txt"));

            // Assert
            this.mockMatcherHelper.Verify(x => x.AddInclude(TranslatePath(@"**\c\*bc.txt")));
            this.mockMatcherHelper.Verify(x => x.Execute(It.Is<DirectoryInfoWrapper>(y => y.FullName.Equals(TranslatePath(@"C:\Users\vanidhi")))));
        }

        [TestMethod]
        public void FilePatternParserShouldCorrectlySplitWithWildCardInMultipleDirectory()
        {
            var patternMatchingResult = new PatternMatchingResult(new List<FilePatternMatch>());
            this.mockMatcherHelper.Setup(x => x.Execute(It.IsAny<DirectoryInfoWrapper>())).Returns(patternMatchingResult);
            this.filePatternParser.GetMatchingFiles(TranslatePath(@"E:\path\to\project\tests\**.Tests\**\*.Tests.dll"));

            // Assert
            this.mockMatcherHelper.Verify(x => x.AddInclude(TranslatePath(@"**.Tests\**\*.Tests.dll")));
            this.mockMatcherHelper.Verify(x => x.Execute(It.Is<DirectoryInfoWrapper>(y => y.FullName.Equals(TranslatePath(@"E:\path\to\project\tests")))));
        }

        [TestMethod]
        public void FilePatternParserShouldCorrectlySplitWithMultpleWildCardInPattern()
        {
            var patternMatchingResult = new PatternMatchingResult(new List<FilePatternMatch>());
            this.mockMatcherHelper.Setup(x => x.Execute(It.IsAny<DirectoryInfoWrapper>())).Returns(patternMatchingResult);
            this.filePatternParser.GetMatchingFiles(TranslatePath(@"E:\path\to\project\tests\Tests*.Blame*.dll"));

            // Assert
            this.mockMatcherHelper.Verify(x => x.AddInclude(TranslatePath(@"Tests*.Blame*.dll")));
            this.mockMatcherHelper.Verify(x => x.Execute(It.Is<DirectoryInfoWrapper>(y => y.FullName.Equals(TranslatePath(@"E:\path\to\project\tests")))));
        }

        [TestMethod]
        public void FilePatternParserShouldCorrectlySplitWithMultpleWildCardInMultipleDirectory()
        {
            var patternMatchingResult = new PatternMatchingResult(new List<FilePatternMatch>());
            this.mockMatcherHelper.Setup(x => x.Execute(It.IsAny<DirectoryInfoWrapper>())).Returns(patternMatchingResult);
            this.filePatternParser.GetMatchingFiles(TranslatePath(@"E:\path\to\project\*tests\Tests*.Blame*.dll"));

            // Assert
            this.mockMatcherHelper.Verify(x => x.AddInclude(TranslatePath(@"*tests\Tests*.Blame*.dll")));
            this.mockMatcherHelper.Verify(x => x.Execute(It.Is<DirectoryInfoWrapper>(y => y.FullName.Equals(TranslatePath(@"E:\path\to\project")))));
        }

        [TestMethod]
        public void FilePatternParserShouldCheckIfFileExistsIfFullPathGiven()
        {
            var patternMatchingResult = new PatternMatchingResult(new List<FilePatternMatch>());
            this.mockFileHelper.Setup(x => x.Exists(TranslatePath(@"E:\path\to\project\tests\Blame.Tests\\abc.Tests.dll"))).Returns(true);
            this.mockMatcherHelper.Setup(x => x.Execute(It.IsAny<DirectoryInfoWrapper>())).Returns(patternMatchingResult);
            var matchingFiles = this.filePatternParser.GetMatchingFiles(TranslatePath(@"E:\path\to\project\tests\Blame.Tests\\abc.Tests.dll"));

            // Assert
            this.mockFileHelper.Verify(x => x.Exists(TranslatePath(@"E:\path\to\project\tests\Blame.Tests\\abc.Tests.dll")));
            Assert.IsTrue(matchingFiles.Contains(TranslatePath(@"E:\path\to\project\tests\Blame.Tests\\abc.Tests.dll")));
        }

        [TestMethod]
        public void FilePatternParserShouldThrowCommandLineExceptionIfFileDoesNotExist()
        {
            var patternMatchingResult = new PatternMatchingResult(new List<FilePatternMatch>());
            this.mockFileHelper.Setup(x => x.Exists(TranslatePath(@"E:\path\to\project\tests\Blame.Tests\\abc.Tests.dll"))).Returns(false);
            this.mockMatcherHelper.Setup(x => x.Execute(It.IsAny<DirectoryInfoWrapper>())).Returns(patternMatchingResult);

            Assert.ThrowsException<TestSourceException>(() => this.filePatternParser.GetMatchingFiles(TranslatePath(@"E:\path\to\project\tests\Blame.Tests\\abc.Tests.dll")));
        }

        private string TranslatePath(string path)
        {
            // RuntimeInformation has conflict when used
            if (Environment.OSVersion.Platform.ToString().StartsWith("Win"))
                return path;
            
            return Regex.Replace(path.Replace("\\", "/"), @"(\w)\:/", @"/mnt/$1/");
        }
    }
}
