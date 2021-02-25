using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Voxelis;
using XNode;

namespace Matryoshka.Generators
{
    // Generators with planting abailities.
    public class TreeBranchInstance : PlanterNodeInstance
    {
        public class TreeSplineNode
        {
            public Vector3 position = Vector3.zero;
            public Quaternion rotation = Quaternion.identity;
            public float radius;
        }

        // HERE: Declear variables for actual calculation
        // e.g. public float ratio;
        public int nodes = 10;

        // Controls the effect of gravity / gnarl
        public float gravityLenthNormalized = 1.5f;
        public float gnarlLenthNormalized = 2.0f;

        float lengthStep;
        public float[] gravityConst = new float[10] { 0.2f, 0.3f, 0.5f, 1.1f, 2.0f, 1.2f, 0.4f, -0.2f, -0.8f, -1.0f };
        public float[] gnarlConst = new float[10] { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };

        // Shape control
        public int iterationStep = 0;
        public float length = 48.0f;
        public float rawStartRadius = 3.0f, endRadiusScale = 0.15f;
        public float rootScale = 1.0f;

        public float noiseAmpl = 8, gravityNoiseAmpl = 8, gnarlNoiseAmpl = 8;

        public Vector3 gravityAxis = Vector3.down;

        // Children control
        public int childGapSeg = 2;
        public int nChildrenPerGap = 3; // How many childs per step
        public int childStartSeg = 3;
        public int childEndSeg = 9;
        public float subSegSpread = 0.05f;
        public float tiltStart = 55f; // in degrees
        public float tiltEnd = 30f;
        public float tiltRand = 5f;
        public float powerStart = 1.0f;
        public float powerEnd = 0.65f;
        public float powerRand = 0.1f;
        public float childRotProgressionRand = 10.0f;
        public float gapRotProgressionRand = 10.0f;
        public float gapRotInit = 0.0f; // Initial rotation of children

        public bool directChild = false;
        public Vector3 childDirection = Vector3.up;
        public float childDirectionStrength = 1.0f;

        public float childRate = 1.0f;

        // Voxelization
        public uint block;

        List<TreeSplineNode> spline = new List<TreeSplineNode>(); // Can we use other stuffs ?
        System.Random random;
        public Utils.PerThreadPool<FastNoiseLite> coherentNoiseGetter;
        FastNoiseLite cnoise;

        public override void Build(World world)
        {
            cnoise = coherentNoiseGetter.Get(Thread.CurrentThread.ManagedThreadId);

            Vector3Int pi = Vector3Int.FloorToInt(origin.position);
            random = new System.Random(pi.x ^ pi.y ^ pi.z);

            spline.Clear();
            for (int i = 0; i < nodes; i++)
            {
                spline.Add(new TreeSplineNode());
            }

            UpdateSpline();
            PlantSeeds();
        }

        private void UpdateSpline()
        {
            float startRadius, endRadius;
            startRadius = rawStartRadius * origin.power;
            endRadius = endRadiusScale * startRadius;

            Vector3 startPos = origin.position;
            Quaternion q = origin.world.rotation;
            lengthStep = length * origin.power / (spline.Count);

            //float[] noiseA = MathCore.PerlinNoise(random, spline.Count, noiseStep, noiseAmpl, null);
            //float[] noiseP = MathCore.PerlinNoise(random, spline.Count, noiseStep, 180, null);

            // Gravity
            //float[] gravityFactor = MathCore.PerlinNoise(random, spline.Count, noiseStep, 0.1f, gravityConst);
            float gravityInfectionStep = gravityLenthNormalized / nodes;

            // Snarl
            //float[] gnarlFactor = MathCore.PerlinNoise(random, spline.Count, noiseStep, 1.0f, gnarlConst);
            float gnarlInfectionStep = gnarlLenthNormalized / nodes;

            ////////////////////////////////////////////////////////////////////////////////////////
            //// Spline Construction
            ////////////////////////////////////////////////////////////////////////////////////////

            Vector3 tangent = new Vector3(Assets.FractalSystemCore.MathCore.RandomRange(random, -1, 1), 0, Utils.MathCore.RandomRange(random, -1, 1)).normalized; // Tangent of previous node

            spline[0].position = startPos;
            spline[0].rotation = q;
            spline[0].radius = startRadius;

            tangent = spline[0].rotation * tangent;

            // Very ugly scale-up of the root, useful for trunks
            spline[0].radius *= rootScale;

            // Construction loop
            for(int i = 1; i < spline.Count; i++)
            {
                /////////////////////////
                //// Noise Samples
                /////////////////////////

                float freq = 3.697f;
                Vector3 curPos_noiseSample = (freq * spline[i - 1].position / (length * origin.power));
                float gravityNoiseValue = gravityNoiseAmpl * cnoise.GetNoise(curPos_noiseSample.x + 25.0f, curPos_noiseSample.y, curPos_noiseSample.z);
                float gnarlNoiseValue = gnarlNoiseAmpl * cnoise.GetNoise(curPos_noiseSample.x - 25.0f, curPos_noiseSample.y + 0.3f, curPos_noiseSample.z - 18.0f);
                float noiseAValue = noiseAmpl * cnoise.GetNoise(curPos_noiseSample.x + 10.0f, curPos_noiseSample.y + 10.0f, curPos_noiseSample.z - 10.0f);
                float noisePValue = 180.0f * cnoise.GetNoise(curPos_noiseSample.x + 15.0f, curPos_noiseSample.y - 3.3f, curPos_noiseSample.z + 4.5f);

                /////////////////////////
                //// General Noise & Initial growth
                /////////////////////////

                Vector3 rotateAxis = Quaternion.AngleAxis(noisePValue, Vector3.up) * tangent;
                rotateAxis.Normalize();

                Vector3 dirc = spline[i - 1].rotation * Quaternion.AngleAxis(noiseAValue, rotateAxis) * Vector3.up;

                /////////////////////////
                //// Gravity
                /////////////////////////

                /* Gravity applied as an addition to regularized direction.
                 * Maybe there're better solutions ......
                 */

                // TODO: gravityConst[i] -> Unity curve
                dirc += gravityAxis * gravityInfectionStep * (gravityNoiseValue + gravityConst[i]);
                dirc.Normalize();

                /////////////////////////
                //// Gnarl (idk if this is natural or not)
                /////////////////////////

                /* Gnarl force will stretch the branch around its origin.
                 * Ideally, a big snarl will let the branch grow like a circle, orbiting its origin.
                 * 
                 * Z forward is used to store parent growth axis - this is not correct. FIXME
                 */

                Vector3 posLocal = spline[i].position - origin.position;
                Vector3 parentTangent = Quaternion.AngleAxis(1.0f, origin.zAxis) * posLocal - posLocal;
                parentTangent.Normalize();

                // TODO: gnarlConst[i] -> Unity curve
                dirc += parentTangent * gnarlInfectionStep * (gnarlNoiseValue + gnarlConst[i]);
                dirc.Normalize();

                /////////////////////////
                //// Calculation for next steps
                /////////////////////////

                spline[i].position = spline[i - 1].position + dirc * lengthStep;
                spline[i].rotation = Quaternion.FromToRotation(Vector3.up, dirc);

                spline[i].radius = ((endRadius - startRadius) * (i / ((float)spline.Count)) + startRadius) * Utils.MathCore.RandomRange(random, 0.9f, 1.1f);
            }
        }

        private void PlantSeeds()
        {
            float dAngle = 360.0f / nChildrenPerGap;

            if (nChildrenPerGap == 1)
            {
                dAngle = 0f;
            }

            float overallAngle = gapRotInit, stepAngle = 0f;

            for (int i = childStartSeg; i <= childEndSeg; i += childGapSeg)
            {
                stepAngle = overallAngle;
                for(int j = 0; j < nChildrenPerGap; j++)
                {
                    // RNG
                    if(random.NextDouble() > childRate)
                    {
                        continue;
                    }

                    /////// Create one seed

                    // Seed growth on current node
                    Vector3 pos = spline[i].position;

                    // Sub-segment position
                    float spreadF = Utils.MathCore.RandomRange(random, -subSegSpread, subSegSpread);

                    // We reached the final node so goback
                    if(i == (nodes - 1))
                    {
                        spreadF = - Mathf.Abs(spreadF);
                    }

                    // Where am I on this branch
                    float branchProgress = (float)i / nodes + spreadF;
                    float childProgress = 1.0f;
                    if(childEndSeg - childStartSeg > 0)
                    {
                        childProgress = ((float)i + spreadF * nodes - childStartSeg) / (childEndSeg - childStartSeg);
                    }

                    /////// Calculate sub-segment position
                    if (spreadF > 0.0f || i == 0)
                    {
                        pos = spline[i].position + spline[i].rotation * Vector3.up *
                            spreadF * length * origin.power;
                    }
                    else
                    {
                        pos = spline[i].position - spline[i - 1].rotation * Vector3.up *
                            (-spreadF) * length * origin.power;
                    }

                    /////// Calculate seed orientation (rotation)
                    float tilt = Utils.MathCore.RandomRange(random, -tiltRand, tiltRand) -
                            (tiltStart - tiltEnd) * childProgress + tiltStart;

                    Quaternion rot = Quaternion.AngleAxis(tilt, Quaternion.AngleAxis(stepAngle, Vector3.up) * Vector3.forward) * spline[i].rotation;
                    
                    if(directChild)
                    {
                        Vector3 childDirc = rot * Vector3.up;
                        childDirc = childDirc.normalized + childDirection * childDirectionStrength;
                        childDirc = childDirc.normalized;
                        rot = Quaternion.FromToRotation(Vector3.up, childDirc);
                    }

                    /////// Calculate seed power

                    float power = origin.power * ((powerEnd - powerStart) * childProgress + powerStart + Utils.MathCore.RandomRange(random, -powerRand, powerRand));

                    // Update stepAngle
                    stepAngle += dAngle + Utils.MathCore.RandomRange(random, -childRotProgressionRand, childRotProgressionRand);

                    seeds.Add(new Seed(pos, rot, power, i * nChildrenPerGap + j));
                }

                // Update overallAngle
                overallAngle += (dAngle / 2f) + Utils.MathCore.RandomRange(random, -gapRotProgressionRand, gapRotProgressionRand);
            }
        }

        public override void Voxelize(World world)
        {
            // HERE: Fill the world with your data
            // Use world.SetBlock(), etc.
            // Beaware of the Bounds of the root node, which can be set in the NodeGraph Editor.
            // NO modifications outside the bound.

            int index;

            for (index = 0; index < (spline.Count - 1); index++)
            {
                TestTree.FillCyclinder(
                    world,
                    block,
                    spline[index].position,
                    spline[index + 1].position,
                    spline[index].radius,
                    spline[index + 1].radius,
                    spline[index].rotation * Vector3.up,
                    spline[index + 1].rotation * Vector3.up
                );
                //break;
            }
        }
    }

    public class TreeBranchUnderNode : GeneratorUnderNode
    {
        // HERE: Declear variables for batched store
        // e.g. public Func<float> ratio;
        
        public int nodes = 10;

        // GRAVITY
        public float[] gravityConst = new float[10] { 0.2f, 0.3f, 0.5f, 1.1f, 2.0f, 1.2f, 0.4f, -0.2f, -0.8f, -1.0f };
        public Vector3 gravityAxis = Vector3.down;
        public float gravityLenthNormalized = 1.5f;

        // GNARL
        public float[] gnarlConst = new float[10] { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
        public float gnarlLenthNormalized = 2.0f;

        // OVERALL SHAPE
        public float rootScale = 1.0f;
        public Func<float> length = C<float>.f(48.0f);
        public Func<float> rawStartRadius = C<float>.f(3.0f);
        public Func<float> endRadiusScale = C<float>.f(0.15f);
        public float noiseAmpl = 8, gravityNoiseAmpl = 8, gnarlNoiseAmpl = 8;

        // CHILDREN DISTRIBUTION
        public int childGapSeg = 2;
        public int nChildrenPerGap = 3; // How many childs per step
        public int childStartSeg = 3;
        public int childEndSeg = 9;
        public float subSegSpread = 0.05f;
        public float tiltStart = 55f; // in degrees
        public float tiltEnd = 30f;
        public float tiltRand = 5f;
        public float powerStart = 1.0f;
        public float powerEnd = 0.65f;
        public float powerRand = 0.1f;
        public float childRotProgressionRand = 10.0f;
        public float gapRotProgressionRand = 10.0f;
        public float gapRotInit = 0.0f; // Initial rotation of children

        public bool directChild = false;
        public Vector3 childDirection = Vector3.up;
        public float childDirectionStrength = 1.0f;
        public float childRate = 1.0f;

        // VOXELIZATION
        public uint block = (uint)Blocks.wood;

        // This creates the actual Instance, i.e. calculation agent.
        public override NodeInstance NewInstance(Seed seed, World world)
        {
            var inst = new TreeBranchInstance();

            // HERE: Assign attributes to your instance
            // e.g. inst.ratio = ratio();

            inst.nodes = nodes;

            inst.gravityConst = gravityConst;
            inst.gravityAxis = gravityAxis;
            inst.gravityLenthNormalized = gravityLenthNormalized;
            inst.gravityNoiseAmpl = gravityNoiseAmpl;

            inst.gnarlConst = gnarlConst;
            inst.gnarlLenthNormalized = gnarlLenthNormalized;
            inst.gnarlNoiseAmpl = gnarlNoiseAmpl;

            inst.rootScale = rootScale;
            inst.length = length();
            inst.rawStartRadius = rawStartRadius();
            inst.endRadiusScale = endRadiusScale();
            inst.noiseAmpl = noiseAmpl;

            inst.childGapSeg = childGapSeg;
            inst.nChildrenPerGap = nChildrenPerGap;
            inst.childStartSeg = childStartSeg;
            inst.childEndSeg = childEndSeg;
            inst.subSegSpread = subSegSpread;
            inst.childRate = childRate;

            inst.tiltStart = tiltStart;
            inst.tiltEnd = tiltEnd;
            inst.tiltRand = tiltRand;

            inst.powerStart = powerStart;
            inst.powerEnd = powerEnd;
            inst.powerRand = powerRand;

            inst.childRotProgressionRand = childRotProgressionRand;
            inst.gapRotProgressionRand = gapRotProgressionRand;
            inst.gapRotInit = gapRotInit;

            inst.directChild = directChild;
            inst.childDirection = childDirection;
            inst.childDirectionStrength = childDirectionStrength;

            inst.block = block;

            inst.coherentNoiseGetter = Utils.NoisePools.OS2S_FBm_3oct_f1;

            return inst;
        }
    }

    public class TreeBranch : BatchedPlanterNode
    {
        // HERE: Declear variables for node graph
        // e.g. [Input] public RoadMap<float> ratio = new C<float>(5.0f);

        [Header("Num of segments")]
        public int nodes = 10;

        // GRAVITY
        [Header("Gravity")]
        public AnimationCurve gravityConst = AnimationCurve.Constant(0.0f, 1.0f, 1.0f);
        public Vector3 gravityAxis = Vector3.down;
        public float gravityLenthNormalized = 1.5f;
        public float gravityNoiseAmpl = 1;

        // GNARL
        [Header("Gnarl")]
        public AnimationCurve gnarlConst = AnimationCurve.Constant(0.0f, 1.0f, 0.0f);
        public float gnarlLenthNormalized = 2.0f;
        public float gnarlNoiseAmpl = 0.2f;

        // OVERALL SHAPE
        [Header("Overall shape")]
        public float rootScale = 1.0f;
        public float noiseAmpl = 8;
        [Input] public RoadMap<float> length = new C<float>(48.0f);
        [Input] public RoadMap<float> rawStartRadius = new C<float>(3.0f);
        [Input] public RoadMap<float> endRadiusScale = new C<float>(0.15f);

        // CHILDREN DISTRIBUTION
        [Space(25.0f)]
        [Header("Children distribution")]
        [Header("Basics")]
        public int childStartSeg = 3;
        public int childEndSeg = 9;
        public int childGapSeg = 2;
        public float subSegSpread = 0.05f;
        public int nChildrenPerGap = 3; // How many childs per step
        [Range(0, 1)]
        public float childRate = 1.0f;

        [Header("Tilt")]
        public float tiltStart = 55f; // in degrees
        public float tiltEnd = 30f;
        public float tiltRand = 5f;
        
        [Header("Power")]
        public float powerStart = 1.0f;
        public float powerEnd = 0.65f;
        public float powerRand = 0.1f;
        
        [Header("Angular distributing")]
        public float childRotProgressionRand = 10.0f;
        public float gapRotProgressionRand = 10.0f;
        public float gapRotInit = 0.0f; // Initial rotation of children

        [Header("Child Direction")]
        public bool directChild = false;
        public Vector3 childDirection = Vector3.up;
        public float childDirectionStrength = 1.0f;

        // VOXELIZATION
        [Header("Voxelization")]
        public uint block = (uint)Blocks.wood;

        // Use this for initialization
        protected override void Init() 
        {
            base.Init();
        }

        // Return the correct value of an output port when requested
        public override object GetValue(NodePort port)
        {
            object v = base.GetValue(port);

            // HERE: Modify if you have additional output ports

            return v;
        }

        // Build() is called when the graph need to populate a "Underlying graph" for actual generation.
        // This should return a UnderNode which can do (batched) calculation.
        public override AbsUnderNode Build()
        {
            TreeBranchUnderNode n = base.Build() as TreeBranchUnderNode;

            // HERE: Retrieve values from the node graph\
            // e.g. n.ratio = GetInputValue("ratio", ratio).realized;

            n.nodes = nodes;

            n.gravityConst = new float[nodes];
            n.gravityAxis = gravityAxis;
            n.gravityLenthNormalized = gravityLenthNormalized;
            n.gravityNoiseAmpl = gravityNoiseAmpl;

            n.gnarlConst = new float[nodes];
            n.gnarlLenthNormalized = gnarlLenthNormalized;
            n.gnarlNoiseAmpl = gnarlNoiseAmpl;

            // Set curves
            for(int i = 0; i < nodes; i++)
            {
                n.gravityConst[i] = gravityConst.Evaluate((float)i / (nodes - 1));
                n.gnarlConst[i] = gnarlConst.Evaluate((float)i / (nodes - 1));
            }

            n.rootScale = rootScale;
            n.noiseAmpl = noiseAmpl;
            n.length = GetInputValue("length", length).realized;
            n.rawStartRadius = GetInputValue("rawStartRadius", rawStartRadius).realized;
            n.endRadiusScale = GetInputValue("endRadiusScale", endRadiusScale).realized;
            n.childRate = childRate;

            n.childGapSeg = childGapSeg;
            n.nChildrenPerGap = nChildrenPerGap;
            n.childStartSeg = childStartSeg;
            n.childEndSeg = childEndSeg;
            n.subSegSpread = subSegSpread;

            n.tiltStart = tiltStart;
            n.tiltEnd = tiltEnd;
            n.tiltRand = tiltRand;

            n.powerStart = powerStart;
            n.powerEnd = powerEnd;
            n.powerRand = powerRand;

            n.childRotProgressionRand = childRotProgressionRand;
            n.gapRotProgressionRand = gapRotProgressionRand;
            n.gapRotInit = gapRotInit;

            n.directChild = directChild;
            n.childDirection = childDirection;
            n.childDirectionStrength = childDirectionStrength;

            n.block = block;

            return n;
        }

        protected override AbsUnderNode NewUndernode()
        {
            return new TreeBranchUnderNode();
        }
    }
}