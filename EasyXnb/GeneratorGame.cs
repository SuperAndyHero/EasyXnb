using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler;
using Microsoft.Xna.Framework.Graphics;
using System.Configuration;
using System.Collections.Specialized;

//untested:
//texture loading in-game
//font compiling
//compressed output
//if profile setting has any effect

//possible future additions:
//build textures coming from models like materials are built
//build animations coming from models



namespace DynamicFontGenerator
{
	public sealed class GeneratorGame : Game
	{
		private readonly GraphicsDeviceManager _graphicsManager;

		private readonly ContentCompiler _contentCompiler;

		private readonly DfgContext _dfgContext;

		private readonly DfgImporterContext _dfgImporterContext;

		public readonly object BuildCoordinator;

		public object ProcessorManagerInstance => GetProcessorManager_BC.GetValue(BuildCoordinator);

		public readonly PropertyInfo GetProcessorManager_BC;

		private readonly MethodInfo _compileMethodInfo;

		public readonly MethodInfo ValidateProcessorTypes_PM;
		public readonly MethodInfo GetInstance_PM;

		public readonly Type BuildCoordinatorType;
		public readonly Type BuildRequestType;
		public readonly Type BuildItemType;

		//public readonly Type importerManagerType;
		//public readonly object importerManager;
		//public readonly MethodInfo GuessFromFilename_IM;


		private readonly ContentImporter<EffectContent> _contentImporter_Effect;
		private readonly ContentImporter<NodeContent> _contentImporter_Node;//new //model importer
		private readonly ContentImporter<TextureContent> _contentImporter_Texture;//new

		private readonly EffectProcessor _effectProcessor;
		private readonly ModelProcessor _modelProcessor;//new
		private readonly TextureProcessor _textureProcessor;//new
		private readonly MaterialProcessor _materialProcessor;//new


		//public readonly Assembly ReLogicPipeLineAssembly;//??
		public readonly Assembly PipeLineAssembly;
		public readonly Assembly Effect_PipeLineAssembly;
		public readonly Assembly FBX_PipeLineAssembly;//new
		public readonly Assembly Texture_PipeLineAssembly;//new

		//settings
		public readonly bool compileFontsSetting;//false
		public readonly bool compileMaterialsSeperateSetting;//false
		public readonly bool compileTexturesSetting;//true
		public readonly bool ignorePngs;//true

		//removed platform setting because the only options are windows, windows phone, and xbox 360
		public readonly GraphicsProfile profileSetting; //GraphicsProfile.Reach
		public readonly bool compressOutputSetting;//false
		public readonly bool rebuildAllSetting;//true //Default value changed from original xnb generator (this was the source of all bugs and the root of all evil)
        public readonly bool closeImmediatelySetting;//false
        public readonly bool waitForInputOnErrorSetting;//true
        public static string inputDirectorySetting;// Environment.CurrentDirectory
		public static string intermedDirectorySetting;// Environment.CurrentDirectory
		public static string outputDirectorySetting;// Environment.CurrentDirectory

		private readonly float modelScale;//false
		private readonly bool modelSwapWindingOrder;//false
		private readonly bool modelGenerateTangentFrames;//false

		private readonly bool outputEffectBytecode;//false

		private readonly string effectExtension;
		private readonly string fontExtension;
		private readonly string textureExtension;
		private readonly string modelExtension;
		private readonly string materialExtension;

		public GeneratorGame()
		{
			#region read config
			compileFontsSetting = bool.Parse(ConfigurationManager.AppSettings.Get("CompileFonts"));
			compileMaterialsSeperateSetting = bool.Parse(ConfigurationManager.AppSettings.Get("CompileMaterialsSeperate"));
			compileTexturesSetting = bool.Parse(ConfigurationManager.AppSettings.Get("CompileTextures"));
			ignorePngs = bool.Parse(ConfigurationManager.AppSettings.Get("IgnorePng"));

			profileSetting = (GraphicsProfile)Enum.Parse(typeof(GraphicsProfile), ConfigurationManager.AppSettings.Get("TargetProfile"));
			compressOutputSetting = bool.Parse(ConfigurationManager.AppSettings.Get("CompressOutput"));

			string inputDir = ConfigurationManager.AppSettings.Get("InputDirectory");
			inputDirectorySetting = (inputDir == "default" ? Environment.CurrentDirectory : inputDir);

			string interDir = ConfigurationManager.AppSettings.Get("IntermediateDirectory");
			intermedDirectorySetting = (interDir == "default" ? Environment.CurrentDirectory : interDir);

			string outDir = ConfigurationManager.AppSettings.Get("OutputDirectory");
			outputDirectorySetting = (outDir == "default" ? Environment.CurrentDirectory : outDir);

			rebuildAllSetting = bool.Parse(ConfigurationManager.AppSettings.Get("RebuildAll"));

			closeImmediatelySetting = bool.Parse(ConfigurationManager.AppSettings.Get("CloseImmediately"));
			waitForInputOnErrorSetting = bool.Parse(ConfigurationManager.AppSettings.Get("WaitForInputOnError"));

            modelScale = float.Parse(ConfigurationManager.AppSettings.Get("ModelScale"));
			modelSwapWindingOrder = bool.Parse(ConfigurationManager.AppSettings.Get("ModelSwapWindingOrder"));
			modelGenerateTangentFrames = bool.Parse(ConfigurationManager.AppSettings.Get("ModelGenerateTangentFrames"));

			outputEffectBytecode = bool.Parse(ConfigurationManager.AppSettings.Get("OutputEffectBytecode"));

			effectExtension = ConfigurationManager.AppSettings.Get("EffectExtension");
			fontExtension = ConfigurationManager.AppSettings.Get("FontExtension");
			textureExtension = ConfigurationManager.AppSettings.Get("TextureExtension");
			modelExtension = ConfigurationManager.AppSettings.Get("ModelExtension");
			materialExtension = ConfigurationManager.AppSettings.Get("MaterialExtension");
			#endregion

			PipeLineAssembly = typeof(ContentCompiler).Assembly;//Non-specific

			Effect_PipeLineAssembly = typeof(EffectImporter).Assembly;//Effect specific
			FBX_PipeLineAssembly = typeof(FbxImporter).Assembly;//Model specific
			Texture_PipeLineAssembly = typeof(TextureImporter).Assembly;//Texture specific //removed if from this since this is needed for materials/models

			Type contentCompilerType = PipeLineAssembly.GetType("Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler.ContentCompiler");//Non-specific
			ConstructorInfo contentCompilerCtor = contentCompilerType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).First();//Non-specific
			_contentCompiler = (ContentCompiler)contentCompilerCtor.Invoke(null);//Non-specific //Uses ctor info to make new instance
			_compileMethodInfo = contentCompilerType.GetMethod("Compile", BindingFlags.Instance | BindingFlags.NonPublic);//Non-specific //gets compile method

			_graphicsManager = new GraphicsDeviceManager(this);
            _graphicsManager.PreparingDeviceSettings += _graphicsManager_PreparingDeviceSettings;
			_dfgContext = new DfgContext(this);
			_dfgImporterContext = new DfgImporterContext();

			BuildCoordinatorType = PipeLineAssembly.GetType("Microsoft.Xna.Framework.Content.Pipeline.BuildCoordinator");
			ConstructorInfo buildCoordinatorCtor = BuildCoordinatorType.GetConstructors().First();
			Type settingsType = PipeLineAssembly.GetType("Microsoft.Xna.Framework.Content.Pipeline.BuildCoordinatorSettings");
            #region settings value
            object settingsValue = Activator.CreateInstance(settingsType);

			var settingsTargetPlatform = settingsType.GetField("TargetPlatform");
				settingsTargetPlatform.SetValue(settingsValue, TargetPlatform.Windows);

			var settingsTargetProfile = settingsType.GetField("TargetProfile");
				settingsTargetProfile.SetValue(settingsValue, profileSetting);

			var settingsCompress = settingsType.GetField("CompressContent");
				settingsCompress.SetValue(settingsValue, compressOutputSetting);

			var settingsRootDir = settingsType.GetField("RootDirectory");
				settingsRootDir.SetValue(settingsValue, inputDirectorySetting);

			var settingsIntermDir = settingsType.GetField("IntermediateDirectory");
				settingsIntermDir.SetValue(settingsValue, intermedDirectorySetting);

			var settingsOutputDir = settingsType.GetField("OutputDirectory");
				settingsOutputDir.SetValue(settingsValue, outputDirectorySetting);

			var settingsRebuildAll = settingsType.GetField("RebuildAll");
				settingsRebuildAll.SetValue(settingsValue, rebuildAllSetting);
			#endregion
			BuildCoordinator = buildCoordinatorCtor.Invoke(new object[] {_dfgContext.Logger, settingsValue, null });
			GetProcessorManager_BC = BuildCoordinatorType.GetProperty("ProcessorManager");

			BuildRequestType = PipeLineAssembly.GetType("Microsoft.Xna.Framework.Content.Pipeline.BuildRequest");
			BuildItemType = PipeLineAssembly.GetType("Microsoft.Xna.Framework.Content.Pipeline.BuildItem");

			//importerManagerType = PipeLineAssembly.GetType("Microsoft.Xna.Framework.Content.Pipeline.ImporterManager");
			//importerManager = importerManagerType.GetConstructors().First().Invoke(new object[] { null });
			//GuessFromFilename_IM = importerManagerType.GetMethod("GuessFromFilename");

			Type processorManagerType = PipeLineAssembly.GetType("Microsoft.Xna.Framework.Content.Pipeline.ProcessorManager");
			ValidateProcessorTypes_PM = processorManagerType.GetMethod("ValidateProcessorTypes");
			GetInstance_PM = processorManagerType.GetMethod("GetInstance");

			_contentImporter_Effect = (ContentImporter<EffectContent>)Activator.CreateInstance(Effect_PipeLineAssembly.GetType("Microsoft.Xna.Framework.Content.Pipeline.EffectImporter"));
				_effectProcessor = new EffectProcessor();

			_contentImporter_Node = (ContentImporter<NodeContent>)Activator.CreateInstance(FBX_PipeLineAssembly.GetType("Microsoft.Xna.Framework.Content.Pipeline.FbxImporter"));
				_modelProcessor = new ModelProcessor
				{//all comments are the default values
					//GenerateMipmaps = true	//unsure of use: (if this generates a texture its likely this is lost, maybe check the processed textures for a difference?)
					//DefaultEffect = MaterialProcessorDefaultEffect.BasicEffect //Any built in xna effect
					//PremultiplyVertexColors = true
					//PremultiplyTextureAlpha = true
					//ResizeTexturesToPowerOfTwo = true //output likely lost
					//ColorKeyColor = new Color(255, 0, 255, 255)
					//ColorKeyEnabled = true
					//TextureFormat = TextureProcessorOutputFormat.DxtCompressed //output likely lost
					//RotationZ = 0f
					//RotationY = 0f
					//RotationX = 0f
					Scale = modelScale, //1f
					SwapWindingOrder = modelSwapWindingOrder, //false
					GenerateTangentFrames = modelGenerateTangentFrames //false
				};
				if (compileMaterialsSeperateSetting) _materialProcessor = new MaterialProcessor();


			if (compileTexturesSetting)
            {
				_contentImporter_Texture = (ContentImporter<TextureContent>)Activator.CreateInstance(Texture_PipeLineAssembly.GetType("Microsoft.Xna.Framework.Content.Pipeline.TextureImporter"));
					_textureProcessor = new TextureProcessor();
					//_textureProcessor.GenerateMipmaps = true;//untested
			}

			base.Content.RootDirectory = "Content";
		}

        private void _graphicsManager_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
			e.GraphicsDeviceInformation.GraphicsProfile = profileSetting;
		}

        protected override void Initialize()
		{
			bool exceptionCaught = false;
			base.Initialize();
            try
            {
                CompileEffects();
				if(compileFontsSetting) CompileFonts();
				if(compileTexturesSetting) CompileTextures();
				CompileModels();
            }
            catch (Exception e)
            {
                exceptionCaught = true;
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine((e.InnerException ?? e).Message);
				if (waitForInputOnErrorSetting)
				{
                    Console.ReadLine();
                    Environment.Exit(0);
                }
            }

            if (!exceptionCaught || !waitForInputOnErrorSetting)
            {
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine("Done! (Closing in 10 seconds)");
				if (!closeImmediatelySetting)
				{
					Thread.Sleep(10000);
				}
				Environment.Exit(0);
			}
		}

		private static void Main()
		{
			using (GeneratorGame generator = new GeneratorGame())
			{
				generator.Run();
				Console.WriteLine();
			}
		}

		private void CompileEffects()
		{
			List<string> list = Directory.EnumerateFiles(inputDirectorySetting, "*.fx").ToList();
			Console.WriteLine("Effect file detected: {0}", list.Count);
			foreach (string item in list)
			{
				string fileName = Path.GetFileName(item);
				Console.WriteLine("* {0}", fileName);
			}
			Console.WriteLine();
			foreach (string item2 in list)
			{
				string fileName2 = Path.GetFileName(item2);
				Console.Write("Start loading effect file: {0}", fileName2);
				EffectContent input = _contentImporter_Effect.Import(item2, _dfgImporterContext);
				Console.WriteLine(" ..Done!");
				string text = Path.GetFileNameWithoutExtension(fileName2) + (outputEffectBytecode ? ".fxc" : effectExtension);//the fxc extension could be a config option like the other extensions
				Console.Write("Start compiling effect.");
				CompiledEffectContent compiledEffectContent = _effectProcessor.Process(input, _dfgContext);
				Console.WriteLine(".Done!");
				Console.Write("Start compiling effect content file: {0}", text);
				if (outputEffectBytecode)
				{
					byte[] bytecode = compiledEffectContent.GetEffectCode();
					File.WriteAllBytes(outputDirectorySetting + "\\" + text, bytecode);
				}
				else
				{
					using (FileStream fileStream = new FileStream(outputDirectorySetting + "\\" + text, FileMode.Create))
					{
						_compileMethodInfo.Invoke(_contentCompiler, new object[7]
						{
						fileStream,
						compiledEffectContent,
						TargetPlatform.Windows,
						profileSetting,
						compressOutputSetting,
						inputDirectorySetting,
						outputDirectorySetting //this param is called referenceRelocationPath. not sure exactly what it does
						});
					}
				}
				Console.WriteLine(" ..Done!");
				Console.WriteLine();
			}
		}

		private void CompileTextures()
		{
            List<string> extension = new List<string>() { "*.jpg", "*.jpeg", "*.bmp", "*.tga", "*.dds" };
			if (!ignorePngs) extension.Add("*.png");
            ParallelQuery<string> list = extension.AsParallel().SelectMany(searchPattern => Directory.EnumerateFiles(inputDirectorySetting, searchPattern));
			Console.WriteLine("Texture file detected: {0}", list.Count<string>());
			foreach (string item in list)
			{
				string fileName = Path.GetFileName(item);
				Console.WriteLine("* {0}", fileName);
			}
			Console.WriteLine();
			foreach (string item2 in list)
			{
				string fileName2 = Path.GetFileName(item2);
				Console.Write("Start loading texture file: {0}", fileName2);
				TextureContent input = _contentImporter_Texture.Import(item2, _dfgImporterContext);
				Console.WriteLine(" ..Done!");
				string text = Path.GetFileNameWithoutExtension(fileName2) + textureExtension;
				Console.Write("Start compiling texture.");
				TextureContent textureContent = _textureProcessor.Process(input, _dfgContext);
				Console.WriteLine(".Done!");
				Console.Write("Start compiling texture content file: {0}", text);
				using (FileStream fileStream = new FileStream(outputDirectorySetting + "\\" + text, FileMode.Create))
				{
					_compileMethodInfo.Invoke(_contentCompiler, new object[7]
					{
						fileStream,
						textureContent,
						TargetPlatform.Windows,
						profileSetting,
						compressOutputSetting,
						inputDirectorySetting,
						outputDirectorySetting //this param is called referenceRelocationPath. not sure exactly what it does
					});
				}
				Console.WriteLine(" ..Done!");
				Console.WriteLine();
			}
		}

		private bool tangents = true;

		private void CompileModels()
		{
			List<string> fileList = Directory.EnumerateFiles(inputDirectorySetting, "*.fbx").ToList();
			Console.WriteLine("Model file detected: {0}", fileList.Count);
			foreach (string name in fileList)
			{
				string fileName = Path.GetFileName(name);
				Console.WriteLine("* {0}", fileName);
			}
			Console.WriteLine();
			foreach (string filepath in fileList)
			{
				string file = Path.GetFileName(filepath);
				string fileName = Path.GetFileNameWithoutExtension(file);
				Console.Write("Start loading model file: {0}", file);
				NodeContent input = _contentImporter_Node.Import(filepath, _dfgImporterContext);
				Console.WriteLine(" ..Done!");
				string outputFile = fileName + modelExtension;
				Console.WriteLine("Start compiling model.");

				ModelContent modelContent = _modelProcessor.Process(input, _dfgContext);//must be before mat

				if (_dfgContext.materialContentCache.Count > 0)//only normal and tex coords here
                {
					int matcount = _dfgContext.materialContentCache.Count;
					int texcount = _dfgContext.processedTextures.Count;
					string matPlural = (matcount > 1 || matcount < 1) ? "s" : "";
					string texturePlural = (texcount > 1 || texcount < 1) ? "s" : "";
					Console.WriteLine("Found {0} material" + matPlural + " that reference {1} texture" + texturePlural + ".", matcount, texcount);
					Console.Write(compileMaterialsSeperateSetting ? "Compiling materials separately." : "Including materials.");
				}

				if (compileMaterialsSeperateSetting)
				{
					foreach (BasicMaterialContent matContent in _dfgContext.materialContentCache)
					{
						MaterialContent matContentOut = _materialProcessor.Process(matContent, _dfgContext);

						string texName = null;
						if (matContent.Texture != null)
						{
							texName = Path.GetFileNameWithoutExtension(matContent.Texture.Filename);
							matContent.Texture = null;
						}

						string nameStart = "(" + fileName + ")_" + matContentOut.Name;
						//string nameEnd = (texName != null ? ((matContentOut.Name.Contains(texName) || matContentOut.Name.Contains(texName.Replace(' ', '_'))) ? "" : ("_(" + texName + ")")) : "");
						string nameEnd = (texName != null ? ("_(" + texName + ")") : "");
						using (FileStream fileStream = new FileStream(nameStart + nameEnd + materialExtension, FileMode.Create))
						{
							_compileMethodInfo.Invoke(_contentCompiler, new object[7]
							{
							fileStream,
							matContentOut,
							TargetPlatform.Windows,
							profileSetting,
							compressOutputSetting,
							inputDirectorySetting,
							outputDirectorySetting //this param is called referenceRelocationPath internally. not sure exactly what it does
							});
						}
					}
				}

                foreach (ModelMeshContent mesh in modelContent.Meshes)
                {
                    foreach (ModelMeshPartContent meshPart in mesh.MeshParts)
                    {
						if(compileMaterialsSeperateSetting)
							meshPart.Material = null;
						else
							((BasicMaterialContent)meshPart.Material).Texture = null;
					}
                    foreach (GeometryContent sourceGeometry in mesh.SourceMesh.Geometry)
                    {
						if(compileMaterialsSeperateSetting)
							sourceGeometry.Material = null;
						else
							((BasicMaterialContent)sourceGeometry.Material).Texture = null;
					}
                }

                Console.WriteLine(".Done!");
				Console.Write("Start compiling model content file: {0}", outputFile);
				using (FileStream fileStream = new FileStream(outputDirectorySetting + "\\" + outputFile, FileMode.Create))
				{
					_compileMethodInfo.Invoke(_contentCompiler, new object[7]
					{
					fileStream,
					modelContent,
					TargetPlatform.Windows,
					profileSetting,
					compressOutputSetting,
					inputDirectorySetting,
					outputDirectorySetting
					});
				}

				_dfgContext.materialContentCache = new List<BasicMaterialContent>();
				_dfgContext.processedTextures = new Dictionary<string, object>();
				Console.WriteLine(" ..Done!");
				Console.WriteLine();
			}
		}

		private void CompileFonts()
		{
			List<string> list = Directory.EnumerateFiles(inputDirectorySetting, "*.dynamicfont").ToList();
			Console.WriteLine("Font Description file detected: {0}", list.Count);
			foreach (string item in list)
			{
				string fileName = Path.GetFileName(item);
				Console.WriteLine("* {0}", fileName);
			}
			Console.WriteLine();
			foreach (string item2 in list)
			{
				string fileName2 = Path.GetFileName(item2);
				Console.Write("Start loading description file: {0}", fileName2);
				EffectContent input = _contentImporter_Effect.Import(item2, _dfgImporterContext);
				Console.WriteLine(" ..Done!");
				string text = Path.GetFileNameWithoutExtension(fileName2) + fontExtension;
				Console.Write("Start compiling font.");
				CompiledEffectContent compiledEffectContent = _effectProcessor.Process(input, _dfgContext);
				Console.WriteLine(".Done!");
				Console.Write("Start compiling font content file: {0}", text);
				using (FileStream fileStream = new FileStream(outputDirectorySetting + "\\" + text, FileMode.Create))
				{
					_compileMethodInfo.Invoke(_contentCompiler, new object[7]
					{
						fileStream,
						compiledEffectContent,
						TargetPlatform.Windows,
						profileSetting,
						compressOutputSetting,
						inputDirectorySetting,
						outputDirectorySetting
					});
				}
				Console.WriteLine(" ..Done!");
				Console.WriteLine();
			}
		}
	}
}
