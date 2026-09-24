using System;
using UnityEngine;

namespace OC.UI.Panel
{
    public abstract class PanelHandler : MonoBehaviour
    {
        public abstract Type ReferenceType { get; }
        public abstract IPanel Create();
    }
}