using System;

using Slb.Ocean.Core;
using Slb.Ocean.Basics;
using Slb.Ocean.Geometry;
using Slb.Ocean.Units;
using Slb.Ocean.Petrel;
using Slb.Ocean.Petrel.UI;
using Slb.Ocean.Petrel.Workflow;
using Slb.Ocean.Petrel.DomainObject;
using Slb.Ocean.Petrel.DomainObject.Seismic;
using System.Collections;
using System.Collections.Generic;

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
                SeismicCube cube = arguments.Cube;
                HorizonInterpretation hzInterp1 = arguments.HzInterpretation1;
                HorizonInterpretation hzInterp2 = arguments.HzInterpretation2;
                int interval = 3;
                Hashtable ht2 = new Hashtable();
                foreach (Point3 item in hzInterp2.GetPoints(cube.SeismicCollection))
                {
                    IndexDouble3 ptIndexDbl = cube.IndexAtPosition(item);
                    Index3 seisIndex = ptIndexDbl.ToIndex3();
                    ht2.Add(seisIndex.I.ToString() + "-" + seisIndex.J.ToString(), ptIndexDbl.K);
                    //PetrelLogger.InfoOutputWindow(ptIndexDbl.I.ToString() + "-" + ptIndexDbl.J.ToString() + "-" + ptIndexDbl.K.ToString());
                    //PetrelLogger.InfoOutputWindow(seisIndex.I.ToString()+"-"+ seisIndex.J.ToString()+"-"+ seisIndex.K.ToString());
                }
                // Make sure we have input arguments
                if (cube == null || hzInterp1 == null || hzInterp2 == null)
                {
                    PetrelLogger.InfoOutputWindow("HelloSeismic: Arguments cannot be empty");
                    return;
                }

                // Make sure we have input arguments
                if (cube.Domain != hzInterp1.Domain)
                {
                    PetrelLogger.InfoOutputWindow("HelloSeismic: Cube and Horizon must be in the same domain");
                    return;
                }
                
                // Start a transaction
                using (ITransaction t = DataManager.NewTransaction())
                {
                    // Create an output horizon property if the user didn't supply one.
                    HorizonInterpretation3D hzInt3D = hzInterp1.GetHorizonInterpretation3D(cube.SeismicCollection);
                    t.Lock(hzInt3D);
                    // Create the property
                    // Template of the cube is the correct template
                    HorizonProperty3D horizonProp3D1 = hzInt3D.CreateProperty(cube.Template);
                    HorizonProperty3D horizonProp3D2 = hzInt3D.CreateProperty(cube.Template);


                    // cache cube indexes
                    Index3 cubeIndex = cube.NumSamplesIJK;
                    List<HorizonProperty3DSample> p1_1 = new List<HorizonProperty3DSample>();
                    List<HorizonProperty3DSample> p1_2 = new List<HorizonProperty3DSample>();
                    // Process the horizon property points.
                    foreach (PointPropertyRecord ppr in horizonProp3D1)
                    {
                        // do not need to initialize the output value, Petrel does this when the property is created initially
                        // ppr.Value = double.NaN;                                             
                        // Get the location for the point
                        Point3 p = ppr.Geometry;
                        // Find the seismic sample at the point
                        IndexDouble3 ptIndexDbl = cube.IndexAtPosition(p);
                        Index3 seisIndex = ptIndexDbl.ToIndex3();
                        object obj = ht2[seisIndex.I.ToString() + "-" + seisIndex.J.ToString()];
                        if (obj==null)
                        {
                            continue;
                        }
                        double z2 = double.Parse(obj.ToString());
                        double z1_1= double.NaN;
                        double z1_2=double.NaN;
                        if (z2!=null)
                        {
                            z1_1 = ptIndexDbl.K-(ptIndexDbl.K - z2) / 3;
                            z1_2 = ptIndexDbl.K - 2*(ptIndexDbl.K - z2) / 3;
                        }
                        // Get the trace containing the seismic sample
                        // SeismicCube.GetTrace(i, j) will throw an exception if the indices (i,j) is out of range.
                        if (seisIndex.I >= 0 && seisIndex.J >= 0 &&
                            seisIndex.I < cubeIndex.I && seisIndex.J < cubeIndex.J)
                        {
                            ITrace trace = cube.GetTrace(seisIndex.I, seisIndex.J);
                            p1_1.Add(new HorizonProperty3DSample(seisIndex.I, seisIndex.J, trace[Convert.ToInt32(z1_1)]));
                            p1_2.Add(new HorizonProperty3DSample(seisIndex.I, seisIndex.J, trace[Convert.ToInt32(z1_2)]));
                        }
                    }
                    horizonProp3D1.Samples = p1_1;
                    horizonProp3D2.Samples = p1_2;
                    // Commit the changes to the data.
                    t.Commit();
                }
                
                return;
            }
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
            private Slb.Ocean.Petrel.DomainObject.Seismic.HorizonInterpretation horizon1;
            private Slb.Ocean.Petrel.DomainObject.Seismic.HorizonInterpretation horizon2;
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

            [Description("Horizon1", "Horizon to drive extraction")]
            public Slb.Ocean.Petrel.DomainObject.Seismic.HorizonInterpretation HzInterpretation1
            {
                internal get { return this.horizon1; }
                set { this.horizon1 = value; }
            }
            [Description("Horizon2", "Horizon to drive extraction")]
            public Slb.Ocean.Petrel.DomainObject.Seismic.HorizonInterpretation HzInterpretation2
            {
                internal get { return this.horizon2; }
                set { this.horizon2 = value; }
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