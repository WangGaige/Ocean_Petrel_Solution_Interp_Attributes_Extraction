using System;
using System.Linq;
using System.Collections.Generic;

using Slb.Ocean.Core;
using Slb.Ocean.Basics;
using Slb.Ocean.Geometry;
using Slb.Ocean.Units;
using Slb.Ocean.Data.Hosting;
using Slb.Ocean.Petrel;
using Slb.Ocean.Petrel.UI;
using Slb.Ocean.Petrel.Workflow;
using Slb.Ocean.Petrel.DomainObject;
using Slb.Ocean.Petrel.DomainObject.Seismic;

namespace OceanLabs.HelloOcean
{
    /// <summary>
    /// This class contains all the methods and subclasses of the HelloSeismic.
    /// Worksteps are displayed in the workflow editor.
    /// </summary>
    public class SeismicInterpretation : Workstep<SeismicInterpretation.Arguments>, IExecutorSource, IAppearance, IDescriptionSource
    {
        #region Overridden Workstep methods

        /// <summary>
        /// Creates an empty Argument instance
        /// </summary>
        /// <returns>New Argument instance.</returns>
        protected override SeismicInterpretation.Arguments CreateArgumentPackageCore(IDataSourceManager dsm)
        {
            return new Arguments(dsm);
        }
        /// <summary>
        /// Copies the Arguments instance.
        /// </summary>
        /// <param name="fromArgumentPackage">the source Arguments instance</param>
        /// <param name="toArgumentPackage">the target Arguments instance</param>
        protected override void CopyArgumentPackageCore(Arguments fromArgumentPackage, Arguments toArgumentPackage)
        {
            DescribedArgumentsHelper.Copy(fromArgumentPackage, toArgumentPackage);
        }
        protected override string UniqueIdCore
        {
            get { return "Slb.OceanTraining.Fundamentals.HelloOcean.SeismicInterpretation"; }
        }
        #endregion

        #region IExecutorSource Members and Executor class

        /// <summary>
        /// Creates the Executor instance for this workstep. This class will do the work of the Workstep.
        /// </summary>
        /// <param name="argumentPackage">the argumentpackage to pass to the Executor</param>
        /// <param name="workflowRuntimeContext">the context to pass to the Executor</param>
        /// <returns>The Executor instance.</returns>
        public Slb.Ocean.Petrel.Workflow.Executor GetExecutor(object argumentPackage, WorkflowRuntimeContext workflowRuntimeContext)
        {
            return new Executor(argumentPackage as Arguments, workflowRuntimeContext);
        }

        public class Executor : Slb.Ocean.Petrel.Workflow.Executor
        {
            Arguments arguments;
            WorkflowRuntimeContext context;

            public Executor(Arguments arguments, WorkflowRuntimeContext context)
            {
                this.arguments = arguments;
                this.context = context;
            }

            public override void ExecuteSimple()
            {
                #region main exercise
                SeismicCube cube = arguments.Cube;
                HorizonInterpretation hzInterp = arguments.HzInterpretation;
                HorizonProperty3D horizonProp3D = arguments.HorizonProp3D;

                // Make sure we have input arguments
                if (cube == null || hzInterp == null)
                {
                    PetrelLogger.InfoOutputWindow("HelloSeismic: Arguments cannot be empty");
                    return;
                }

                // Make sure we have input arguments
                if (cube.Domain != hzInterp.Domain)
                {
                    PetrelLogger.InfoOutputWindow("HelloSeismic: Cube and Horizon must be in the same domain");
                    return;
                }

                // Start a transaction
                using (ITransaction t = DataManager.NewTransaction())
                {
                    // Create an output horizon property if the user didn't supply one.
                    if (horizonProp3D == null)
                    {
                        // get 3D part of the interpretation corresponding to cube's seismic collection
                        HorizonInterpretation3D hzInt3D = hzInterp.GetHorizonInterpretation3D(cube.SeismicCollection);
                        if (hzInt3D == null)
                        {
                            PetrelLogger.InfoOutputWindow("HelloSeismic: Unable to get Horizon Interpretation 3D from HzInt");
                            return;
                        }
                        t.Lock(hzInt3D);
                        // Create the property
                        // Template of the cube is the correct template
                        horizonProp3D = hzInt3D.CreateProperty(cube.Template);
                    }
                    else
                    {
                        // Lock the property so we can update it
                        t.Lock(horizonProp3D);
                    }

                    // cache cube indexes
                    Index3 cubeIndex = cube.NumSamplesIJK;

                    // Process the horizon property points.
                    foreach (PointPropertyRecord ppr in horizonProp3D)
                    {
                        // do not need to initialize the output value, Petrel does this when the property is created initially
                        // ppr.Value = double.NaN;

                        // Get the location for the point
                        Point3 p = ppr.Geometry;

                        // Find the seismic sample at the point
                        IndexDouble3 ptIndexDbl = cube.IndexAtPosition(p);
                        Index3 seisIndex = ptIndexDbl.ToIndex3();

                        // Get the trace containing the seismic sample
                        // SeismicCube.GetTrace(i, j) will throw an exception if the indices (i,j) is out of range.
                        if (seisIndex.I >= 0 && seisIndex.J >= 0 &&
                            seisIndex.I < cubeIndex.I && seisIndex.J < cubeIndex.J)
                        {
                            ITrace trace = cube.GetTrace(seisIndex.I, seisIndex.J);

                            // trace[k] will throw an exception if the index k is out of range.
                            // Set the property value to the corresponding trace sample value. 
                            if (seisIndex.K >= 0 && seisIndex.K < cubeIndex.K)
                                ppr.Value = trace[seisIndex.K];
                        }
                    }

                    // Commit the changes to the data.
                    t.Commit();

                    // Set up the output argument value 
                    arguments.HorizonProp3D = horizonProp3D;
                }
                #endregion 

                #region Challenging part, body
                //
                // Compute an attribute for all fault's interpretations under the SeismicProject
                //

                // Navigate to SeismicProject and loop through all collections
                SeismicRoot sr = SeismicRoot.Get(PetrelProject.PrimaryProject);
                SeismicProject sp = sr.SeismicProject;
                // First part of lab checked for a cube, so project exists at this point,
                // else something like: if (sp == SeismicProject.NullObject) return;

                // some object can be transaction locked inside, need to commit before exit
                using (ITransaction tr = DataManager.NewTransaction())
                {
                    foreach (InterpretationCollection ic in sp.InterpretationCollections)
                        loopIC(ic, cube, tr);
                    tr.Commit();
                }
                #endregion

                return;
            }

            #region Challenging part, helpers

            //
            // Loop across all FaultInterpretation within a InterpretationCollection, 
            // then check for sub-collections
            private void loopIC(InterpretationCollection ic, SeismicCube cube, ITransaction tr)
            {
                foreach (FaultInterpretation fi in ic.FaultInterpretations) calcFI(fi, cube, tr);
                foreach (InterpretationCollection ics in ic.InterpretationCollections) loopIC(ics, cube, tr);
                return;
            }

            //
            // Check FaultProperty with particular name and template, create if doesn't exist
            //
            private FaultProperty findOrCreateFProperty(FaultInterpretation fi, Template t, string Name, ITransaction tr)
            {
                FaultProperty fp = fi.FaultProperties.Where(prp => (prp.Name == Name && prp.Template == t)).FirstOrDefault();
                if (fp == null)
                {
                    fp = fi.CreateProperty(t);
                    fp.Name = Name;
                }
                else
                {
                    tr.Lock(fp);
                }
                return fp;
            }


            //
            // Loop across all points in FaultInterpretation polylines:
            // 1. set context to Cube
            // 2. set attribute value from Cube (the same as for Horizon in main exercise)
            //
            private void calcFI(FaultInterpretation fi, SeismicCube cube, ITransaction tr)
            {
                // Check for Domain
                if (cube.Domain != fi.Domain) return;
                // Lock FaultInterpretation for update
                tr.Lock(fi);
                FaultProperty fp = findOrCreateFProperty(fi, cube.Template, "Ocean Fault Property", tr);

                // Set Cube as a context for FaultInterpretation 
                List<FaultInterpretationPolyline> chgdPolylines = new List<FaultInterpretationPolyline>();
                IEnumerable<FaultInterpretationPolyline> fiPolylines = fi.GetPolylines();

                // Process each poly line and set the context for each point.
                foreach (FaultInterpretationPolyline poly in fiPolylines)
                {
                    foreach (FaultInterpretationContextPoint pt in poly)
                    {
                        // Must cast as Context is IDomainObject and can be SeismicLine2D or SeismicCube.
                        pt.Context = (IDomainObject)cube;
                    }
                    chgdPolylines.Add(poly);
                }
                fi.SetPolylines(chgdPolylines);


                // set attribute's values via PointPropertyRecord, the same way as for Horizon

                Index3 cubeIndex = cube.NumSamplesIJK;
                foreach (PointPropertyRecord ppr in fp)
                {
                    ppr.Value = double.NaN;
                    Point3 p = ppr.Geometry;
                    IndexDouble3 ptIndexDbl = cube.IndexAtPosition(p);
                    Index3 seisIndex = ptIndexDbl.ToIndex3();
                    if (seisIndex.I >= 0 && seisIndex.J >= 0 &&
                        seisIndex.I < cubeIndex.I && seisIndex.J < cubeIndex.J)
                    {
                        ITrace trace = cube.GetTrace(seisIndex.I, seisIndex.J);
                        if (seisIndex.K >= 0 && seisIndex.K < cubeIndex.K)
                            ppr.Value = trace[seisIndex.K];
                    }
                }
                return;
            }

            #endregion
        }

        #endregion
        

        /// <summary>
        /// ArgumentPackage class for HelloSeismic.
        /// Each public property is an argument in the package.  The name, type and
        /// input/output role are taken from the property and modified by any
        /// attributes applied.
        /// </summary>
        public class Arguments : DescribedArgumentsByReflection
        {
            private Slb.Ocean.Petrel.DomainObject.Seismic.SeismicCube cube;
            private Slb.Ocean.Petrel.DomainObject.Seismic.HorizonInterpretation horizon;
            private Slb.Ocean.Petrel.DomainObject.Seismic.HorizonProperty3D horizonProperty;

            public Arguments()
                : this(DataManager.DataSourceManager)
            {
            }

            public Arguments(IDataSourceManager dataSourceManager)
            {
            }

            [Description("Cube", "Seismic cube to extract data from")]
            public Slb.Ocean.Petrel.DomainObject.Seismic.SeismicCube Cube
            {
                internal get { return this.cube; }
                set { this.cube = value; }
            }

            [Description("Horizon", "Horizon to drive extraction")]
            public Slb.Ocean.Petrel.DomainObject.Seismic.HorizonInterpretation HzInterpretation
            {
                internal get { return this.horizon; }
                set { this.horizon = value; }
            }

            [Description("Horizon Property", "Extracted data stored in property")]
            public Slb.Ocean.Petrel.DomainObject.Seismic.HorizonProperty3D HorizonProp3D
            {
                get { return this.horizonProperty; }
                set { this.horizonProperty = value; }
            }
        }

        #region IAppearance Members

        public event EventHandler<TextChangedEventArgs> TextChanged { add { } remove { } }
        public event EventHandler<ImageChangedEventArgs> ImageChanged { add { } remove { } }

        public string Text
        {
            get { return Description.Name; }
        }

        public System.Drawing.Bitmap Image
        {
            get { return PetrelImages.Modules; }
        }

        #endregion

        #region IDescriptionSource Members

        /// <summary>
        /// Gets the description of the HelloSeismic
        /// </summary>
        public IDescription Description
        {
            get { return SeismicInterpretationDescription.Instance; }
        }

        /// <summary>
        /// This singleton class contains the description of the HelloSeismic.
        /// Contains Name, Shorter description and detailed description.
        /// </summary>
        public class SeismicInterpretationDescription : IDescription
        {
            /// <summary>
            /// Contains the singleton instance.
            /// </summary>
            private static SeismicInterpretationDescription instance = new SeismicInterpretationDescription();
            /// <summary>
            /// Gets the singleton instance of this Description class
            /// </summary>
            public static SeismicInterpretationDescription Instance
            {
                get { return instance; }
            }

            #region IDescription Members

            /// <summary>
            /// Gets the name of HelloSeismic
            /// </summary>
            public string Name
            {
                get { return "SeismicInterpretation"; }
            }
            /// <summary>
            /// Gets the short description of HelloSeismic
            /// </summary>
            public string ShortDescription
            {
                get { return "Seismic amplitude extraction"; }
            }
            /// <summary>
            /// Gets the detailed description of HelloSeismic
            /// </summary>
            public string Description
            {
                get { return "Extract seismic amplitudes along a horizon"; }
            }

            #endregion
        }
        #endregion
    }
}