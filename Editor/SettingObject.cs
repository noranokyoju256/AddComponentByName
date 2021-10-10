using UnityEngine;
using System.Collections.Generic;
using System;

namespace Norakyo.Internal
{
    internal sealed class SettingObject : ScriptableObject
    {
        [SerializeField]
        private List<KeyAndTypesString> _dictionary = new List<KeyAndTypesString>();

        public List<KeyAndTypesString> Dict => _dictionary;

        [Serializable]
        public class KeyAndTypesString
        {
            [SerializeField]
            private string _key;
            [SerializeField]
            private string _types;
            public string Key => _key;
            public string TypesString => _types;
        }
    }
}

