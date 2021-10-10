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
                // .asset �����݂��Ȃ��ꍇ�̓C���X�^���X��V�K�쐬����
                if (!File.Exists(AssetPath))
                {
                    _settingObj = ScriptableObject.CreateInstance<SettingObject>();
                    return _settingObj;
                }

                // .asset �����݂���ꍇ�� .asset ��ǂݍ���
                _settingObj = InternalEditorUtility
                        .LoadSerializedFileAndForget(AssetPath)
                        .OfType<SettingObject>()
                        .FirstOrDefault()
                    ;

                // .asset ���s���Ȍ`���œǂݍ��ނ��Ƃ��ł��Ȃ������ꍇ��
                // �C���X�^���X��V�K�쐬����
                if (_settingObj == null)
                {
                    _settingObj = ScriptableObject.CreateInstance<SettingObject>();
                }
                return _settingObj;
            }
        }

        /// <summary>
        /// �G�f�B�^���������ɌĂ΂��
        /// </summary>
        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            //�q�G�����L�[�E�B���h�E�ɕύX����������Ƃ��ɌĂ΂��R�[���o�b�N
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            //���ׂẴR���|�[�l���g���擾���ăL���b�V�����Ă���
            _componentTypes = GetComponentTypes();
        }

        /// <summary>
        /// Preference�ɐݒ�p�^�u��ǉ�����
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
        /// �ݒ��ʂ̕`��
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

                // �p�����[�^���ҏW���ꂽ�ꍇ�� �C���X�^���X�ɔ��f����
                // �Ȃ����� .asset �t�@�C���Ƃ��Ă��ۑ�����
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
        /// �q�G�����L�[���ύX���ꂽ�Ƃ��ɌĂ΂��
        /// </summary>
        private static void OnHierarchyChanged()
        {
            //�I�𒆂̃I�u�W�F�N�g���擾
            var gameObject = Selection.activeGameObject;
            if (gameObject == null)
            {
                return;
            }
            
            var name = gameObject.name;
            //"_"�ŕ���
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
                        && type.IsSubclassOf(typeof(Component))                 //type���R���|�[�l���g�ł���
                        && !gameObject.TryGetComponent(type, out Component _))  //���̃R���|�[�l���g���܂��t�^����Ă��Ȃ�
                    {
                        gameObject.AddComponent(type);
                    }
                }
            }

        }


        /// <summary>
        /// ����̖��O�̎��ɑΉ�����R���|�[�l���g���擾����
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static IEnumerable<Type> GetTypesBySpecificName(string name)
        {
            var types = new List<Type>();

            foreach (var pair in SettingObject.Dict.Where(x => x.Key == name))
            {
                //�X�y�[�X������
                var nonSpaceString = pair.TypesString.Replace(" ", "");
                //','�ŕ���
                foreach (var typeName in nonSpaceString.Split(','))
                {
                    var type = GetTypeByFullName(typeName);
                    //�t���l�[���őΉ�����^������ꍇ�͂����D��
                    if (type != null)
                    {
                        types.Add(type);
                        continue;
                    }
                    //�Ȃ��ꍇ�͖��O��Ԃ��������^�̖��O����
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
        /// �������O�̃R���|�[�l���g���擾����
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static IEnumerable<Type> GetTypesByName(string name)
        {
            var types = _componentTypes.Where(x => x.Name == name);
            return types.Any() ? types : null;
        }

        /// <summary>
        /// �������O�̃R���|�[�l���g���擾����(FullName�w��)
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static Type GetTypeByFullName(string name)
        {
            return _componentTypes.Where(x => x.FullName == name).FirstOrDefault();
        }

        /// <summary>
        /// ���ׂẴR���|�[�l���g���擾����
        /// </summary>
        /// <returns></returns>
        private static List<Type> GetComponentTypes()
        {
            var types = new List<Type>();

            //���ׂẴR���|�[�l���g��ID���擾����
            var idArray = Unsupported.GetSubmenusCommands("Component");
            //���������̕������\�����K�\��
            var regex = new Regex("^\\d+$");

            foreach (var commandString in idArray)
            {
                Type type = null;

                //UnityEngine�̃N���X�ȊO
                if (commandString.StartsWith("SCRIPT"))
                {
                    //�C���X�^���XID����^���擾����
                    var instanceID = int.Parse(commandString.Substring(6));
                    var obj = EditorUtility.InstanceIDToObject(instanceID);
                    var monoScript = obj as MonoScript;
                    type = monoScript.GetClass();
                }
                //UnityEngine�̃N���X
                else if (regex.IsMatch(commandString))
                {
                    //�N���XID����^���擾����
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
        /// ClassID����^���擾����
        /// </summary>
        /// <param name="classID"></param>
        /// <returns></returns>
        private static Type GetTypeByClassID(int classID)
        {
            //"UnityType"�N���X���擾����iinternal�ȃN���X�ł���j
            //�ȉ���URL����v���O�����������
            //https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Editor/Mono/TypeSystem/UnityType.cs
            var unityType = Assembly.GetAssembly(typeof(MonoScript)).GetType("UnityEditor.UnityType");

            //"FindTypeByPersistentTypeID"���\�b�h���ClassID����UnityType���擾����
            var classObject = unityType.InvokeMember("FindTypeByPersistentTypeID", BindingFlags.InvokeMethod, null, null, new object[] { classID });
            if (classObject == null)
            {
                return null;
            }

            //���ۂ̌^�̖��O��"name"�v���p�e�B����擾�ł���
            var name = unityType.GetProperty("name").GetValue(classObject) as string;

            //���ׂẴA�Z���u�����疼�O����v����^��T��
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
