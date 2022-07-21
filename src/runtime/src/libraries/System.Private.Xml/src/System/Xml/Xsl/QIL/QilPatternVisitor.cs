// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace System.Xml.Xsl.Qil
{
    /// <summary>
    /// Pattern visitor base internal class
    /// </summary>
    internal abstract class QilPatternVisitor : QilReplaceVisitor
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public QilPatternVisitor(QilPatterns patterns, QilFactory f) : base(f)
        {
            Patterns = patterns;
        }

        public QilPatterns Patterns { get; set; }

        public int Threshold { get; set; } = int.MaxValue;
        public int ReplacementCount { get; private set; }

        public int LastReplacement { get; private set; }

        public bool Matching
        {
            get { return ReplacementCount < Threshold; }
        }

        /// <summary>
        /// Called when a pattern has matched, but before the replacement code is executed.  If this
        /// method returns false, then the replacement code is skipped.
        /// </summary>
        protected virtual bool AllowReplace(int pattern, QilNode original)
        {
            // If still matching patterns,
            if (Matching)
            {
                // Increment the replacement count
                ReplacementCount++;

                // Save the id of this pattern in case it's the last
                LastReplacement = pattern;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Called when a pattern has matched and after replacement code is executed.
        /// </summary>
        protected virtual QilNode Replace(int pattern, QilNode original, QilNode replacement)
        {
            replacement.SourceLine = original.SourceLine;
            return replacement;
        }

        /// <summary>
        /// Called when all replacements have already been made and all annotations are complete.
        /// </summary>
        [return: NotNullIfNotNull("node")]
        protected virtual QilNode? NoReplace(QilNode? node)
        {
            return node;
        }


        //-----------------------------------------------
        // QilVisitor overrides
        //-----------------------------------------------

        /// <summary>
        /// Visit children of this node first, then pattern match on the node itself.
        /// </summary>
        protected override QilNode Visit(QilNode node)
        {
            if (node == null)
                return VisitNull()!;

            node = VisitChildren(node);
            return base.Visit(node);
        }

        // Do not edit this region
        #region AUTOGENERATED
        #region meta
        protected override QilNode VisitQilExpression(QilExpression n) { return NoReplace(n); }
        protected override QilNode VisitFunctionList(QilList n) { return NoReplace(n); }
        protected override QilNode VisitGlobalVariableList(QilList n) { return NoReplace(n); }
        protected override QilNode VisitGlobalParameterList(QilList n) { return NoReplace(n); }
        protected override QilNode VisitActualParameterList(QilList n) { return NoReplace(n); }
        protected override QilNode VisitFormalParameterList(QilList n) { return NoReplace(n); }
        protected override QilNode VisitSortKeyList(QilList n) { return NoReplace(n); }
        protected override QilNode VisitBranchList(QilList n) { return NoReplace(n); }
        protected override QilNode VisitOptimizeBarrier(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitUnknown(QilNode n) { return NoReplace(n); }
        #endregion

        #region specials
        protected override QilNode VisitDataSource(QilDataSource n) { return NoReplace(n); }
        protected override QilNode VisitNop(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitError(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitWarning(QilUnary n) { return NoReplace(n); }
        #endregion

        #region variables
        protected override QilNode VisitFor(QilIterator n) { return NoReplace(n); }
        protected override QilNode VisitForReference(QilIterator n) { return NoReplace(n); }
        protected override QilNode VisitLet(QilIterator n) { return NoReplace(n); }
        protected override QilNode VisitLetReference(QilIterator n) { return NoReplace(n); }
        protected override QilNode VisitParameter(QilParameter n) { return NoReplace(n); }
        protected override QilNode VisitParameterReference(QilParameter n) { return NoReplace(n); }
        protected override QilNode VisitPositionOf(QilUnary n) { return NoReplace(n); }
        #endregion

        #region literals
        protected override QilNode VisitTrue(QilNode n) { return NoReplace(n); }
        protected override QilNode VisitFalse(QilNode n) { return NoReplace(n); }
        protected override QilNode VisitLiteralString(QilLiteral n) { return NoReplace(n); }
        protected override QilNode VisitLiteralInt32(QilLiteral n) { return NoReplace(n); }
        protected override QilNode VisitLiteralInt64(QilLiteral n) { return NoReplace(n); }
        protected override QilNode VisitLiteralDouble(QilLiteral n) { return NoReplace(n); }
        protected override QilNode VisitLiteralDecimal(QilLiteral n) { return NoReplace(n); }
        protected override QilNode VisitLiteralQName(QilName n) { return NoReplace(n); }
        protected override QilNode VisitLiteralType(QilLiteral n) { return NoReplace(n); }
        protected override QilNode VisitLiteralObject(QilLiteral n) { return NoReplace(n); }
        #endregion

        #region boolean operators
        protected override QilNode VisitAnd(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitOr(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitNot(QilUnary n) { return NoReplace(n); }
        #endregion

        #region choice
        protected override QilNode VisitConditional(QilTernary n) { return NoReplace(n); }
        protected override QilNode VisitChoice(QilChoice n) { return NoReplace(n); }
        #endregion

        #region collection operators
        protected override QilNode VisitLength(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitSequence(QilList n) { return NoReplace(n); }
        protected override QilNode VisitUnion(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitIntersection(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitDifference(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitAverage(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitSum(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitMinimum(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitMaximum(QilUnary n) { return NoReplace(n); }
        #endregion

        #region arithmetic operators
        protected override QilNode VisitNegate(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitAdd(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitSubtract(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitMultiply(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitDivide(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitModulo(QilBinary n) { return NoReplace(n); }
        #endregion

        #region string operators
        protected override QilNode VisitStrLength(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitStrConcat(QilStrConcat n) { return NoReplace(n); }
        protected override QilNode VisitStrParseQName(QilBinary n) { return NoReplace(n); }
        #endregion

        #region value comparison operators
        protected override QilNode VisitNe(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitEq(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitGt(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitGe(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitLt(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitLe(QilBinary n) { return NoReplace(n); }
        #endregion

        #region node comparison operators
        protected override QilNode VisitIs(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitAfter(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitBefore(QilBinary n) { return NoReplace(n); }
        #endregion

        #region loops
        protected override QilNode VisitLoop(QilLoop n) { return NoReplace(n); }
        protected override QilNode VisitFilter(QilLoop n) { return NoReplace(n); }
        #endregion

        #region sorting
        protected override QilNode VisitSort(QilLoop n) { return NoReplace(n); }
        protected override QilNode VisitSortKey(QilSortKey n) { return NoReplace(n); }
        protected override QilNode VisitDocOrderDistinct(QilUnary n) { return NoReplace(n); }
        #endregion

        #region function definition and invocation
        protected override QilNode VisitFunction(QilFunction n) { return NoReplace(n); }
        protected override QilNode VisitFunctionReference(QilFunction n) { return NoReplace(n); }
        protected override QilNode VisitInvoke(QilInvoke n) { return NoReplace(n); }
        #endregion

        #region XML navigation
        protected override QilNode VisitContent(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitAttribute(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitParent(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitRoot(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitXmlContext(QilNode n) { return NoReplace(n); }
        protected override QilNode VisitDescendant(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitDescendantOrSelf(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitAncestor(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitAncestorOrSelf(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitPreceding(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitFollowingSibling(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitPrecedingSibling(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitNodeRange(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitDeref(QilBinary n) { return NoReplace(n); }
        #endregion

        #region XML construction
        protected override QilNode VisitElementCtor(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitAttributeCtor(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitCommentCtor(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitPICtor(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitTextCtor(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitRawTextCtor(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitDocumentCtor(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitNamespaceDecl(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitRtfCtor(QilBinary n) { return NoReplace(n); }
        #endregion

        #region Node properties
        protected override QilNode VisitNameOf(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitLocalNameOf(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitNamespaceUriOf(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitPrefixOf(QilUnary n) { return NoReplace(n); }
        #endregion

        #region Type operators
        protected override QilNode VisitTypeAssert(QilTargetType n) { return NoReplace(n); }
        protected override QilNode VisitIsType(QilTargetType n) { return NoReplace(n); }
        protected override QilNode VisitIsEmpty(QilUnary n) { return NoReplace(n); }
        #endregion

        #region XPath operators
        protected override QilNode VisitXPathNodeValue(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitXPathFollowing(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitXPathPreceding(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitXPathNamespace(QilUnary n) { return NoReplace(n); }
        #endregion

        #region XSLT
        protected override QilNode VisitXsltGenerateId(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitXsltInvokeLateBound(QilInvokeLateBound n) { return NoReplace(n); }
        protected override QilNode VisitXsltInvokeEarlyBound(QilInvokeEarlyBound n) { return NoReplace(n); }
        protected override QilNode VisitXsltCopy(QilBinary n) { return NoReplace(n); }
        protected override QilNode VisitXsltCopyOf(QilUnary n) { return NoReplace(n); }
        protected override QilNode VisitXsltConvert(QilTargetType n) { return NoReplace(n); }
        #endregion

        #endregion


        //-----------------------------------------------
        // Helper methods
        //-----------------------------------------------

        /// <summary>
        /// A bit vector holding a set of rewrites.
        /// </summary>
        internal sealed class QilPatterns
        {
            private readonly BitArray _bits;

            public QilPatterns(int szBits, bool allSet)
            {
                _bits = new BitArray(szBits, allSet);
            }

            public void Add(int i)
            {
                _bits.Set(i, true);
            }

            public bool IsSet(int i)
            {
                return _bits[i];
            }
        }
    }
}