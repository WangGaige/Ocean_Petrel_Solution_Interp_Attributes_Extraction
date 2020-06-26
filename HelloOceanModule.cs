using System;
using Slb.Ocean.Core;
using Slb.Ocean.Petrel;
using Slb.Ocean.Petrel.UI;
using Slb.Ocean.Petrel.Workflow;

namespace OceanLabs.HelloOcean
{
    /// <summary>
    /// This class will control the lifecycle of the Module.
    /// The order of the methods are the same as the calling order.
    /// </summary>
    public class HelloOceanModule : IModule
    {
        public HelloOceanModule()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        #region IModule Members

        /// <summary>
        /// This method runs once in the Module life; when it loaded into the petrel.
        /// This method called first.
        /// </summary>
        public void Initialize()
        {
            // TODO:  Add HelloOceanModule.Initialize implementation
        }

        /// <summary>
        /// This method runs once in the Module life. 
        /// In this method, you can do registrations of the not UI related components.
        /// (eg: datasource, plugin)
        /// </summary>
        public void Integrate()
        {
            // Registrations:
            SeismicInterpretation seismicInterpretationInstance = new SeismicInterpretation();
            PetrelSystem.WorkflowEditor.Add(seismicInterpretationInstance);
            PetrelSystem.ProcessDiagram.Add(new WorkstepProcessWrapper(seismicInterpretationInstance), "Ocean labs");
        }

        /// <summary>
        /// This method runs once in the Module life. 
        /// In this method, you can do registrations of the UI related components.
        /// (eg: settingspages, treeextensions)
        /// </summary>
        public void IntegratePresentation()
        {
            // Registrations:
            PetrelSystem.ConfigurationService.AddConfiguration(ResourceLabs.OceanFundamentalsCourseConfig);


            // TODO:  Add HelloOceanModule.IntegratePresentation implementation
        }

        /// <summary>
        /// This method called once in the life of the module; 
        /// right before the module is unloaded. 
        /// It is usually when the application is closing.
        /// </summary>
        public void Disintegrate()
        {
            // TODO:  Add HelloOceanModule.Disintegrate implementation
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            // TODO:  Add HelloOceanModule.Dispose implementation
        }

        #endregion

    }


}