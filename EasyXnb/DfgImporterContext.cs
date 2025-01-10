using System;
using Microsoft.Xna.Framework.Content.Pipeline;
using xnbcompiler.EasyXnb;

namespace DynamicFontGenerator
{
	public sealed class DfgImporterContext : ContentImporterContext
	{
		public BuildLogger BuildLogger = new BuildLogger();
		public override ContentBuildLogger Logger => BuildLogger;

        public override string OutputDirectory => GeneratorGame.outputDirectorySetting;

        public override string IntermediateDirectory => GeneratorGame.intermedDirectorySetting;

        public override void AddDependency(string filename)
		{
			throw new NotImplementedException();
		}
	}
}
