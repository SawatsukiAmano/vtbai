﻿
using Microsoft.VisualBasic;
using System.Runtime.Serialization;
using System.Xml.Linq;
using Tomlyn;

namespace Model
{
    public  class ConfigModel
    {

        private const string ConfigPath = "data/config/config.toml";

        private const string SensitivePath = "data/config/sensitive_words.txt";


        public static ConfigModel ConfigToml = Toml.ToModel<ConfigModel>(FileHelper.ReadAllText(ConfigPath));

        public static List<string> SensitiveWords { get; private set; }
        public static List<string> SensitiveWordsPY { get; private set; }


        public string Env { get; set; }

        public string ApiListen { get; set; }


        public static ConfigModel NewM()
        {
            string str = FileHelper.ReadAllText(ConfigPath);
            return Toml.ToModel<ConfigModel>(str);
        }

        public static void RefreshConfig()
        {
            ConfigToml = Toml.ToModel<ConfigModel>(FileHelper.ReadAllText(ConfigPath));
            SensitiveWords = FileHelper.ReadAllLines(SensitivePath).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            SensitiveWordsPY = new List<string>();
            foreach (var s in SensitiveWords)
            {
                SensitiveWordsPY.Add(PinyinHelper.Hanzi2Pinyin(s));
            }
        }

        public  LiveConf Live { get; set; }

        public  GptConf Gpt { get; set; }

        public TtsConf Tts { get; set; }

        public L2ddConf L2d { get; set; }

        public AigcConf Aigc { get;set; }

        public class LiveConf
        {
            public  BiliConf Bili { get;  set; }
            public  DouyinConf Douyin { get;  set; }

            public string Platform { get;  set; }

            public class BiliConf
            {
                public string Roomid { get;  set; }
                public List<string> Topid { get;  set; }

            }
            public class DouyinConf
            {
                public string Roomid { get; private set; }

            }
        }

        public class GptConf
        {
            public string platform { get;  set; }

            public OpenaiConf Openai { get;  set; }

            public Glm6bConf Glm6b { get; set; }

            public OtherConf Other { get; set; }
            public class OpenaiConf
            {
                public string key { get;  set; }

                public string Nya1 { get;  set; }

                public string ProxyDomain { get;  set; }

                public int MaxContext { get;  set; }

                public string Model { get;  set; }
            }

            public class Glm6bConf { 
            
            }

            public class OtherConf
            {
                public string TransmitUrl { get; set; }

                [DataMember(Name = "q_name")]
                public string QName { get; set; }
            }
        }

        public class TtsConf { 

            public string Platform { get; set; }

            public string TextIntervalMs { get; set; }

            public int MaxTextLength { get; set; }

            public int MaxTtsLength { get; set; }

            public int MaxWavQueue { get; set; }
            public bool AutoDelWav { get; set; }

            public MoegoeConf Moegoe { get; set; }

            public class MoegoeConf
            {
                public string ModelOnnx { get; set; }

                public string ModelConfig { get; set; }

                public string ModelPth { get; set; }

                public int SpeakerId { get; set; }

                public decimal LengthScale { get; set; }

                public decimal NoiseScale { get; set; }

                public decimal NoiseScaleW { get; set; }
            }
        }

        public class L2ddConf { 
        public string Platform { get; set; }
        }

        public class AigcConf { 
        
        }
    }
}