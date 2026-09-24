using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OC.UI.Interactions
{
    public class MaterialChanger : MonoBehaviour, ISceneVisual
    {
        private List<Renderer> _renderers;
        private List<Material> _originalMaterials;
        private List<MaterialPropertyBlock> _originalPropertyBlocks;
        private static readonly int Color = Shader.PropertyToID("_BaseColor");
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        
        protected void OnEnable()
        {
            GetOriginalMaterials();
        }
        
        public void Hide(bool hide)
        {
            foreach (var item in _renderers)
            {
                item.enabled = !hide;
            }
        }
        
        public void SetMaterial(Material material)
        {
            foreach (var item in _renderers)
            {
                var materials = item.materials;
                for (var i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }
                item.materials = materials;
            }
        }

        protected void SetColor(Color color, float alpha)
        {
            foreach (var item in _renderers)
            {
                for (var i = 0; i < item.materials.Length; i++)
                {
                    var property = new MaterialPropertyBlock();
                    item.GetPropertyBlock(property,i);
                    color.a = alpha;
                    property.SetColor(Color, color);
                    item.SetPropertyBlock(property,i);
                }
            }
        }
        
        protected void ResetColor()
        {
            var index = 0; 
            foreach (var item in _renderers)
            {
                for (var i = 0; i < item.materials.Length; i++)
                {
                    item.SetPropertyBlock(_originalPropertyBlocks[index],i);
                    index++;
                }
            }
        }
        
        private void GetOriginalMaterials()
        {
            _renderers = GetComponentsInChildren<Renderer>().ToList();
            _originalMaterials = new List<Material>();
            _originalPropertyBlocks = new List<MaterialPropertyBlock>();
            
            foreach (var item in _renderers)
            {
                for (var i = 0; i < item.materials.Length; i++)
                {
                    _originalMaterials.Add(item.materials[i]);
                    var property = new MaterialPropertyBlock();
                    item.GetPropertyBlock(property, i);
                    _originalPropertyBlocks.Add(property);
                }
            }
        }

        protected void SetOriginalMaterials()
        {
            var index = 0; 
            foreach (var item in _renderers)
            {
                var materials = item.materials;
                for (var i = 0; i < materials.Length; i++)
                {
                    materials[i] = _originalMaterials[index];
                    index++;
                }
                item.materials = materials;
            }
        }
    }
}
