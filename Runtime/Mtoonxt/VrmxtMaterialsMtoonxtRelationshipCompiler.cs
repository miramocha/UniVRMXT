using System;
using System.Collections.Generic;
using UniVRMXT.Format;

namespace UniVRMXT.Mtoonxt
{
    public enum VrmxtMtoonxtCoverageMode
    {
        None = 0,
        FullSceneWithoutWriters = 1,
        FullReaderSilhouette = 2,
    }

    public sealed class VrmxtMtoonxtRelationshipPass
    {
        public VrmxtMtoonxtRelationshipPass(
            string comp,
            string pass,
            string zTest,
            bool zWrite,
            bool cullBack
        )
        {
            Comp = comp;
            Pass = pass;
            ZTest = zTest;
            ZWrite = zWrite;
            CullBack = cullBack;
        }

        public string Comp { get; }
        public string Pass { get; }
        public string ZTest { get; }
        public bool ZWrite { get; }
        public bool CullBack { get; }
    }

    public sealed class VrmxtMtoonxtRelationshipPlan
    {
        public VrmxtMtoonxtRelationshipPlan(
            VrmxtMaterialsMtoonxtRelationship source,
            int localRef,
            VrmxtMtoonxtRelationshipPass writerPrimary,
            VrmxtMtoonxtRelationshipPass writerSecondary,
            VrmxtMtoonxtRelationshipPass reader,
            bool writersStampMask,
            bool readersStampMask,
            VrmxtMtoonxtCoverageMode coverageMode
        )
        {
            Source = source;
            LocalRef = localRef;
            WriterPrimary = writerPrimary;
            WriterSecondary = writerSecondary;
            Reader = reader;
            WritersStampMask = writersStampMask;
            ReadersStampMask = readersStampMask;
            CoverageMode = coverageMode;
        }

        public VrmxtMaterialsMtoonxtRelationship Source { get; }
        public int LocalRef { get; }
        public VrmxtMtoonxtRelationshipPass WriterPrimary { get; }
        public VrmxtMtoonxtRelationshipPass WriterSecondary { get; }
        public VrmxtMtoonxtRelationshipPass Reader { get; }
        public bool WritersStampMask { get; }
        public bool ReadersStampMask { get; }
        public VrmxtMtoonxtCoverageMode CoverageMode { get; }
    }

    /// <summary>
    /// Maps the portable relationship graph to the Unity pass choreography confirmed by
    /// the Blender/Unity M01-M10 and A01-A03 parity matrix.
    /// </summary>
    public static class VrmxtMaterialsMtoonxtRelationshipCompiler
    {
        public static List<VrmxtMtoonxtRelationshipPlan> Compile(
            IReadOnlyList<VrmxtMaterialsMtoonxtRelationship> relationships,
            int firstLocalRef
        )
        {
            var result = new List<VrmxtMtoonxtRelationshipPlan>();
            if (relationships == null)
            {
                return result;
            }

            var nextRef = Math.Max(1, firstLocalRef);
            for (var i = 0; i < relationships.Count && nextRef <= 255; i++)
            {
                var relationship = relationships[i];
                if (relationship == null)
                {
                    continue;
                }

                result.Add(CompileOne(relationship, nextRef));
                nextRef++;
            }

            return result;
        }

        private static VrmxtMtoonxtRelationshipPlan CompileOne(
            VrmxtMaterialsMtoonxtRelationship relationship,
            int localRef
        )
        {
            var writerDepth = relationship.WriterDepthTest;
            var readerDepth = relationship.ReaderDepthTest;
            var cullBack = relationship.WritersSelfOcclude;

            if (relationship.WritersOnlyInsideReaders)
            {
                return new VrmxtMtoonxtRelationshipPlan(
                    relationship,
                    localRef,
                    Subject(
                        "equal",
                        relationship.ShowWritersThroughOccluders ? "always" : writerDepth,
                        relationship.WritersWriteDepth,
                        cullBack
                    ),
                    null,
                    Mask(readerDepth, relationship.ReadersWriteDepth),
                    writersStampMask: false,
                    readersStampMask: true,
                    coverageMode: relationship.IgnoreOccludedReaderAreas
                        ? VrmxtMtoonxtCoverageMode.None
                        : VrmxtMtoonxtCoverageMode.FullReaderSilhouette
                );
            }

            if (relationship.WritersOnlyOutsideReaders)
            {
                var backgroundOnly = relationship.ShowWritersThroughOccluders;
                return new VrmxtMtoonxtRelationshipPlan(
                    relationship,
                    localRef,
                    Subject(
                        "notEqual",
                        writerDepth,
                        relationship.WritersWriteDepth,
                        cullBack
                    ),
                    null,
                    backgroundOnly
                        ? null
                        : Mask(readerDepth, relationship.ReadersWriteDepth),
                    writersStampMask: false,
                    readersStampMask: !backgroundOnly,
                    coverageMode: backgroundOnly
                        ? VrmxtMtoonxtCoverageMode.FullSceneWithoutWriters
                        : (
                            relationship.IgnoreOccludedReaderAreas
                                ? VrmxtMtoonxtCoverageMode.None
                                : VrmxtMtoonxtCoverageMode.FullReaderSilhouette
                        )
                );
            }

            if (relationship.ShowWritersThroughOccluders)
            {
                return new VrmxtMtoonxtRelationshipPlan(
                    relationship,
                    localRef,
                    Subject(
                        "notEqual",
                        writerDepth,
                        relationship.WritersWriteDepth,
                        cullBack
                    ),
                    Subject(
                        "equal",
                        "always",
                        relationship.WritersWriteDepth,
                        cullBack
                    ),
                    Mask(readerDepth, relationship.ReadersWriteDepth),
                    writersStampMask: false,
                    readersStampMask: true,
                    coverageMode: relationship.IgnoreOccludedReaderAreas
                        ? VrmxtMtoonxtCoverageMode.None
                        : VrmxtMtoonxtCoverageMode.FullReaderSilhouette
                );
            }

            var readerComp = string.Equals(
                relationship.Comparison,
                VrmxtMaterialsMtoonxtRelationships.ComparisonInside,
                StringComparison.Ordinal
            )
                ? "equal"
                : "notEqual";
            return new VrmxtMtoonxtRelationshipPlan(
                relationship,
                localRef,
                Mask(writerDepth, relationship.WritersWriteDepth),
                null,
                Subject(
                    readerComp,
                    readerDepth,
                    relationship.ReadersWriteDepth,
                    cullBack: false
                ),
                writersStampMask: true,
                readersStampMask: false,
                coverageMode: VrmxtMtoonxtCoverageMode.None
            );
        }

        private static VrmxtMtoonxtRelationshipPass Mask(string zTest, bool zWrite)
        {
            return new VrmxtMtoonxtRelationshipPass(
                "always",
                "replace",
                zTest,
                zWrite,
                cullBack: false
            );
        }

        private static VrmxtMtoonxtRelationshipPass Subject(
            string comp,
            string zTest,
            bool zWrite,
            bool cullBack
        )
        {
            return new VrmxtMtoonxtRelationshipPass(comp, "keep", zTest, zWrite, cullBack);
        }
    }
}
