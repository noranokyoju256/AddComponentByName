using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Norakyo.Internal
{
    internal static class AddComponentByName
    {
        private static List<Type> _componentTypes = new List<Type>();
        private static SettingObject _settingObj;
        private static string AssetPath => "ProjectSettings/Norakyo/AddComponentByName";
        private static SettingObject SettingObject
        {
            get
            {
                if (_settingObj)
                {
                    return _settingObj;
                }
                // .asset が存在しない場合はインスタンスを新規作成する
                if (!File.Exists(AssetPath))
                {
                    _settingObj = ScriptableObject.CreateInstance<SettingObject>();
                    return _settingObj;
                }

                // .asset が存在する場合は .asset を読み込む
                _settingObj = InternalEditorUtility
                        .LoadSerializedFileAndForget(AssetPath)
                        .OfType<SettingObject>()
                        .FirstOrDefault()
                    ;

                // .asset が不正な形式で読み込むことができなかった場合は
                // インスタンスを新規作成する
                if (_settingObj == null)
                {
                    _settingObj = ScriptableObject.CreateInstance<SettingObject>();
                }
                return _settingObj;
            }
        }

        /// <summary>
        /// エディタ初期化時に呼ばれる
        /// </summary>
        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            //ヒエラルキーウィンドウに変更が加わったときに呼ばれるコールバック
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            //すべてのコンポーネントを取得してキャッシュしておく
            _componentTypes = GetComponentTypes();
        }

        /// <summary>
        /// Preferenceに設定用タブを追加する
        /// </summary>
        /// <returns></returns>
        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            var instance = SettingObject;
            var serializedObject = new SerializedObject(instance);
            var keywords = SettingsProvider.GetSearchKeywordsFromSerializedObject(serializedObject);

            var provider = new SettingsProvider("Norakyo/", SettingsScope.User)
            {
                label = "AddComponentByName",
                guiHandler = GUIHandler,
                keywords = keywords
            };

            return provider;
        }

        /// <summary>
        /// 設定画面の描画
        /// </summary>
        /// <param name="searchContext"></param>
        private static void GUIHandler(string searchContext)
        {
            var instance = SettingObject;
            var editor = Editor.CreateEditor(instance);

            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                var serializedObject = editor.serializedObject;

                serializedObject.Update();

                editor.DrawDefaultInspector();


                if (!scope.changed) return;

                // パラメータが編集された場合は インスタンスに反映して
                // なおかつ .asset ファイルとしても保存する
                serializedObject.ApplyModifiedProperties();

                var directoryPath = Path.GetDirectoryName(AssetPath);

                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                InternalEditorUtility.SaveToSerializedFileAndForget
                (
                    obj: new[] { editor.target },
                    path: AssetPath,
                    allowTextSerialization: true
                );
            }
        }

        /// <summary>
        /// ヒエラルキーが変更されたときに呼ばれる
        /// </summary>
        private static void OnHierarchyChanged()
        {
            //選択中のオブジェクトを取得
            var gameObject = Selection.activeGameObject;
            if (gameObject == null)
            {
                return;
            }
            
            var name = gameObject.name;
            //"_"で分割
            var fragments = Regex.Split(name, "_");

            foreach (var typeName in fragments)
            {
                var types = GetTypesBySpecificName(typeName) ?? GetTypesByName(typeName);

                if(types == null)
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (type != null
                        && type.IsSubclassOf(typeof(Component))                 //typeがコンポーネントである
                        && !gameObject.TryGetComponent(type, out Component _))  //そのコンポーネントがまだ付与されていない
                    {
                        gameObject.AddComponent(type);
                    }
                }
            }

        }


        /// <summary>
        /// 特定の名前の時に対応するコンポーネントを取得する
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static IEnumerable<Type> GetTypesBySpecificName(string name)
        {
            var types = new List<Type>();

            foreach (var pair in SettingObject.Dict.Where(x => x.Key == name))
            {
                //スペースを消す
                var nonSpaceString = pair.TypesString.Replace(" ", "");
                //','で分割
                foreach (var typeName in nonSpaceString.Split(','))
                {
                    var type = GetTypeByFullName(typeName);
                    //フルネームで対応する型がある場合はそれを優先
                    if (type != null)
                    {
                        types.Add(type);
                        continue;
                    }
                    //ない場合は名前空間を除いた型の名前から
                    else
                    {
                        var typesByName = GetTypesByName(typeName);
                        if (typesByName != null)
                        {
                            types.AddRange(typesByName);
                            continue;
                        }
                    }
                }
            }

            if(types.Count > 0)
            {
                return types;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 同じ名前のコンポーネントを取得する
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static IEnumerable<Type> GetTypesByName(string name)
        {
            var types = _componentTypes.Where(x => x.Name == name);
            return types.Any() ? types : null;
        }

        /// <summary>
        /// 同じ名前のコンポーネントを取得する(FullName指定)
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static Type GetTypeByFullName(string name)
        {
            return _componentTypes.Where(x => x.FullName == name).FirstOrDefault();
        }

        /// <summary>
        /// すべてのコンポーネントを取得する
        /// </summary>
        /// <returns></returns>
        private static List<Type> GetComponentTypes()
        {
            var types = new List<Type>();

            //すべてのコンポーネントのIDを取得する
            var idArray = Unsupported.GetSubmenusCommands("Component");
            //数字だけの文字列を表す正規表現
            var regex = new Regex("^\\d+$");

            foreach (var commandString in idArray)
            {
                Type type = null;

                //UnityEngineのクラス以外
                if (commandString.StartsWith("SCRIPT"))
                {
                    //インスタンスIDから型を取得する
                    var instanceID = int.Parse(commandString.Substring(6));
                    var obj = EditorUtility.InstanceIDToObject(instanceID);
                    var monoScript = obj as MonoScript;
                    type = monoScript.GetClass();
                }
                //UnityEngineのクラス
                else if (regex.IsMatch(commandString))
                {
                    //クラスIDから型を取得する
                    var classID = int.Parse(commandString);
                    type = GetTypeByClassID(classID);
                }

                if (type != null)
                {
                    types.Add(type);
                }
            }

            return types;
        }

        /// <summary>
        /// ClassIDから型を取得する
        /// </summary>
        /// <param name="classID"></param>
        /// <returns></returns>
        private static Type GetTypeByClassID(int classID)
        {
            //"UnityType"クラスを取得する（internalなクラスである）
            //以下のURLからプログラムが見れる
            //https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Editor/Mono/TypeSystem/UnityType.cs
            var unityType = Assembly.GetAssembly(typeof(MonoScript)).GetType("UnityEditor.UnityType");

            //"FindTypeByPersistentTypeID"メソッドよりClassIDからUnityTypeを取得する
            var classObject = unityType.InvokeMember("FindTypeByPersistentTypeID", BindingFlags.InvokeMethod, null, null, new object[] { classID });
            if (classObject == null)
            {
                return null;
            }

            //実際の型の名前は"name"プロパティから取得できる
            var name = unityType.GetProperty("name").GetValue(classObject) as string;

            //すべてのアセンブリから名前が一致する型を探す
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (name == type.Name)
                    {
                        return type;
                    }
                }
            }

            return null;
        }
    }
}
