using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    public class MaterialWrapper {
        private Material _material;

        private MarkedStack<KeyValuePair<int, Texture>> _textureStack;
        private MarkedStack<KeyValuePair<int, Color>> _colorStack;
        private MarkedStack<KeyValuePair<string, bool>> _keywordStack;

        public MaterialWrapper(Material material) {
            _material = material;

            _textureStack = new MarkedStack<KeyValuePair<int, Texture>>();
            _colorStack = new MarkedStack<KeyValuePair<int, Color>>();
            _keywordStack = new MarkedStack<KeyValuePair<string, bool>>();
        }

        public void SetTexture(int hash, Texture texture) {
            Texture current = _material.GetTexture(hash);
            _textureStack.Push(new KeyValuePair<int, Texture>(hash, current));
            _material.SetTexture(hash, texture);
        }

        public void RestoreTextures() {
            while (_textureStack.Count > 0 && !_textureStack.AtMark()) {
                KeyValuePair<int, Texture> kvp = _textureStack.Pop();
                int hash = kvp.Key;
                Texture texture = kvp.Value;
                _material.SetTexture(hash, texture);
            }
            _textureStack.RemoveMark();
        }

        public void SetColor(int hash, Color color) {
            Color current = _material.GetColor(hash);
            _colorStack.Push(new KeyValuePair<int, Color>(hash, current));
            _material.SetColor(hash, color);
        }

        public void RestoreColors() {
            while (_colorStack.Count > 0 && !_colorStack.AtMark()) {
                KeyValuePair<int, Color> kvp = _colorStack.Pop();
                int hash = kvp.Key;
                Color color = kvp.Value;
                _material.SetColor(hash, color);
            }
            _colorStack.RemoveMark();
        }

        public void EnableKeyword(string keyword) {
            bool current = _material.IsKeywordEnabled(keyword);
            _keywordStack.Push(new KeyValuePair<string, bool>(keyword, current));
            _material.EnableKeyword(keyword);
        }

        public void DisableKeyword(string keyword) {
            bool current = _material.IsKeywordEnabled(keyword);
            _keywordStack.Push(new KeyValuePair<string, bool>(keyword, current));
            _material.DisableKeyword(keyword);
        }

        public void RestoreKeywords() {
            while (_keywordStack.Count > 0 && !_keywordStack.AtMark()) {
                KeyValuePair<string, bool> kvp = _keywordStack.Pop();
                string keyword = kvp.Key;
                bool enabled = kvp.Value;
                if (enabled) {
                    _material.EnableKeyword(keyword);
                } else {
                    _material.DisableKeyword(keyword);
                }
            }
            _keywordStack.RemoveMark();
        }

        public void Mark() {
            _textureStack.Mark();
            _colorStack.Mark();
            _keywordStack.Mark();
        }

        public void RestoreChanges() {
            RestoreTextures();
            RestoreColors();
            RestoreKeywords();
        }

        private class MarkedStack<T> : Stack<T> {
            public MarkedStack() : base() {
                _markers = new Stack<int>();
            }

            private Stack<int> _markers;

            public bool AtMark() {
                return _markers.Count > 0 && _markers.Peek() == this.Count;
            }

            public void Mark() {
                _markers.Push(this.Count - 1);
            }

            public void RemoveMark() {
                _markers.Pop();
            }
        }
    }
}
