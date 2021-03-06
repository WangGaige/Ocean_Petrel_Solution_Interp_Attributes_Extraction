using System;
using System.Collections.Generic;
using Slb.Ocean.Core;

namespace OceanLabs.HelloOcean
{
    public class HelloOceanPlugin : Slb.Ocean.Core.Plugin
    {
        public override string AppVersion
        {
            get { return "2018.1"; }
        }

        public override string Author
        {
            get { return "Ocean Training and Support"; }
        }

        public override string Contact
        {
            get { return "oceantraining@slb.com"; }
        }

        public override IEnumerable<PluginIdentifier> Dependencies
        {
            get { return null; }
        }

        public override string Description
        {
            get { return "new Ocean plugin for training class"; }
        }

        public override string ImageResourceName
        {
            get { return null; }
        }

        public override Uri PluginUri
        {
            get { return new Uri("http://www.ocean.slb.com"); }
        }

        public override IEnumerable<ModuleReference> Modules
        {
            get 
            {
                // Please fill this method with your modules with lines like this:
                //yield return new ModuleReference(typeof(Module));
                yield return new ModuleReference(typeof(HelloOceanModule));
            }
        }

        public override string Name
        {
            get { return "HelloOcean Lab Plugin"; }
        }

        public override PluginIdentifier PluginId
        {
            get { return new PluginIdentifier(typeof(HelloOceanPlugin).FullName, typeof(HelloOceanPlugin).Assembly.GetName().Version); }
        }

        public override ModuleTrust Trust
        {
            get { return new ModuleTrust("Default"); }
        }

        public override string DeploymentFolder
        {
            get
            {
                return "OceanTraining";
            }
        }
    }
}
