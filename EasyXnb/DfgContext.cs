using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Graphics;
using System.Reflection;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using System.IO;

namespace DynamicFontGenerator
{
	public sealed class DfgContext : ContentProcessorContext
	{


		private readonly GeneratorGame _g;

		private readonly DfgLogger _logger;

		public override ContentBuildLogger Logger => _logger;

		public override OpaqueDataDictionary Parameters
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public override TargetPlatform TargetPlatform => TargetPlatform.Windows;

		public override GraphicsProfile TargetProfile => _g.GraphicsDevice.GraphicsProfile;

		public override string BuildConfiguration => "";

		public override string OutputFilename
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public override string OutputDirectory => GeneratorGame.outputDirectorySetting;

		public override string IntermediateDirectory => GeneratorGame.intermedDirectorySetting;

		public DfgContext(GeneratorGame g)
		{
			_g = g;
			_logger = new DfgLogger();
		}

		public override void AddDependency(string filename)
		{
			throw new NotImplementedException();
		}

		public override void AddOutputFile(string filename)
		{
			throw new NotImplementedException();
		}

		public override TOutput BuildAndLoadAsset<TInput, TOutput>(ExternalReference<TInput> sourceAsset, string processorName, OpaqueDataDictionary processorParameters, string importerName)
		{
			throw new NotImplementedException();
		}

		//public List<ExternalReference<TextureContent>> textureContentCache = new List<ExternalReference<TextureContent>>();
		//public List<string> textureContentCache = new List<string>();
		public List<BasicMaterialContent> materialContentCache = new List<BasicMaterialContent>();

		public override ExternalReference<TOutput> BuildAsset<TInput, TOutput>(ExternalReference<TInput> sourceAsset, string processorName, OpaqueDataDictionary processorParameters, string importerName, string assetName)
		{
			//I dont think this actually does anything, but is here to give the expected result
			object buildItem = RequestBuild<TInput, TOutput>(sourceAsset, assetName, importerName, processorName, processorParameters, 0);
			string absolutePath = (string)_g.BuildCoordinatorType.GetMethod("GetAbsolutePath").Invoke(_g.BuildCoordinator, new object[] { _g.BuildItemType.GetField("OutputFilename").GetValue(buildItem)});
			return new ExternalReference<TOutput>(absolutePath);
		}

		public Dictionary<string, object> processedTextures = new Dictionary<string, object>();

		private object RequestBuild<TInput, TOutput>(ExternalReference<TInput> sourceAsset, string assetName, string importerName, string processorName, OpaqueDataDictionary processorParameters, int buildOptions)
		{
			string name = Path.GetFileNameWithoutExtension(sourceAsset.Filename);
			if (!processedTextures.ContainsKey(name))
			{
				//I dont think this actually does anything, but is here to give the expected result
				if (sourceAsset == null)
				{
					throw new ArgumentNullException("sourceAsset");
				}
				if (string.IsNullOrEmpty(sourceAsset.Filename))
				{
					throw new ArgumentNullException("sourceAsset.Filename");
				}
				if (!string.IsNullOrEmpty(processorName))
				{
					_g.ValidateProcessorTypes_PM.Invoke(_g.ProcessorManagerInstance, new object[] { processorName, typeof(TInput), typeof(TOutput) });
				}
				object buildRequest = Activator.CreateInstance(_g.BuildRequestType);
				_g.BuildRequestType.GetField("SourceFilename").SetValue(buildRequest, Directory.GetCurrentDirectory() + "\\" + Path.GetFileName(sourceAsset.Filename));//ew but also Im lazy
				_g.BuildRequestType.GetField("AssetName").SetValue(buildRequest, Path.GetFileNameWithoutExtension(sourceAsset.Filename));
				_g.BuildRequestType.GetField("ImporterName").SetValue(buildRequest, "TextureImporter");//_g.GuessFromFilename_IM.Invoke(_g.importerManager, new object[] { sourceAsset.Filename }));
				_g.BuildRequestType.GetField("ProcessorName").SetValue(buildRequest, processorName);
				_g.BuildRequestType.GetField("BuildOptions").SetValue(buildRequest, buildOptions);

				if (processorParameters != null)
				{
					FieldInfo processParams = _g.BuildRequestType.GetField("ProcessorParameters");
					OpaqueDataDictionary dict = (OpaqueDataDictionary)processParams.GetValue(buildRequest);
					foreach (KeyValuePair<string, object> processorParameter in processorParameters)
					{
						dict.Add(processorParameter.Key, processorParameter.Value);
					}
				}
				object buildItemOut = _g.BuildCoordinatorType.GetMethod("RequestBuild", new[] { _g.BuildRequestType }).Invoke(_g.BuildCoordinator, new object[] { buildRequest });
				processedTextures.Add(name, buildItemOut);
				return buildItemOut;
			}
			return processedTextures[name];
		}

		public override TOutput Convert<TInput, TOutput>(TInput input, string processorName, OpaqueDataDictionary processorParameters)
		{

			//I dont think this actually does anything, but is here to give the expected result
			if (input == null)
			{
				throw new ArgumentNullException("input");
			}
			if (string.IsNullOrEmpty(processorName))
			{
				throw new ArgumentNullException("processorName");
			}
            
			_g.ValidateProcessorTypes_PM.Invoke(_g.ProcessorManagerInstance, new object[] { processorName, typeof(TInput), typeof(TOutput) });
			IContentProcessor instance = (IContentProcessor)_g.GetInstance_PM.Invoke(_g.ProcessorManagerInstance, new object[] { processorName, processorParameters, null, _logger });
			var output = instance.Process(input, this);

			BasicMaterialContent mat = (BasicMaterialContent)output;
			materialContentCache.Add(mat);
			return (TOutput)output;
		}
	}
}
