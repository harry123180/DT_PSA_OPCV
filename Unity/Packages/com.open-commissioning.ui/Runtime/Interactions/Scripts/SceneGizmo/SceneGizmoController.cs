using UnityEngine;

namespace OC.UI.Interactions.SceneGizmo
{
	public class SceneGizmoController : MonoBehaviour
	{
		private const int GIZMOS_LAYER = 24;
		[SerializeField]
		public Camera _gizmoCamera;
		private Transform _gizmoCamParent;

		[SerializeField]
		private Renderer[] _gizmoComponents;

		[SerializeField]
		private TextMesh[] _labels;
		private Transform[] _labelTransforms;

		private Transform _referenceTransform;

		private Vector3 _prevForward;

		private Material _gizmoDefaultMaterial, _gizmoFadeMaterial, _gizmoHighlightMaterial;
		private int _gizmoMaterialFadeProperty;

		private GizmoComponent _highlightedComponent = GizmoComponent.None;
		private GizmoComponent _fadingComponent = GizmoComponent.None;
		private bool _isFadingToZero;
		private float _fadeT = 1f;

		private bool _updateTargetTexture;
		public RenderTexture TargetTexture { get; private set; }



		private void Awake()
		{
			_gizmoCamParent = _gizmoCamera.transform.parent;
			_labelTransforms = new Transform[_labels.Length];

			int gizmoResolution = Mathf.Min( Mathf.NextPowerOfTwo( Mathf.Max( Screen.width, Screen.height ) / 6 ), 512 );
			TargetTexture = new RenderTexture( gizmoResolution, gizmoResolution, 16 );
			_gizmoCamera.aspect = 1f;
			_gizmoCamera.targetTexture = TargetTexture;
			_gizmoCamera.cullingMask = 1 << GIZMOS_LAYER;
			_gizmoCamera.eventMask = 0;
			_gizmoCamera.enabled = false;

			_gizmoDefaultMaterial = _gizmoComponents[0].sharedMaterial;
			_gizmoFadeMaterial = new Material( _gizmoDefaultMaterial );
			_gizmoMaterialFadeProperty = Shader.PropertyToID( "_AlphaVal" );

			_gizmoHighlightMaterial = new Material( _gizmoDefaultMaterial );
			_gizmoHighlightMaterial.EnableKeyword( "HIGHLIGHT_ON" );
			_gizmoHighlightMaterial.color = Color.yellow;

			for( int i = 0; i < _gizmoComponents.Length; i++ )
				_gizmoComponents[i].gameObject.layer = GIZMOS_LAYER;

			for( int i = 0; i < _labelTransforms.Length; i++ )
			{
				_labels[i].gameObject.layer = GIZMOS_LAYER;
				_labelTransforms[i] = _labels[i].transform;
			}

            if (!_referenceTransform)
            {
                _referenceTransform = Camera.main.transform;

				if (!_referenceTransform)
				{
					Debug.LogError("ReferenceTransform mustn't be null!");
				}
				else
				{
					Camera referenceCamera = _referenceTransform.GetComponent<Camera>();
					if (referenceCamera != null)
					{
						referenceCamera.cullingMask = referenceCamera.cullingMask & ~(1 << GIZMOS_LAYER);
						if (referenceCamera.clearFlags == CameraClearFlags.Color)
						{
							Color cameraBg = referenceCamera.backgroundColor;
							cameraBg.a = 0f;
							_gizmoCamera.backgroundColor = cameraBg;
						}
					}
				}
            }
        }

		private void OnEnable()
		{
			if( _highlightedComponent != GizmoComponent.None )
			{
				_gizmoComponents[(int) _highlightedComponent].sharedMaterial = _gizmoDefaultMaterial;
				_highlightedComponent = GizmoComponent.None;
			}

			SetHiddenComponent( GizmoComponent.None );
			_updateTargetTexture = true;
		}

		private void OnDestroy()
		{
			if( TargetTexture != null )
			{
				TargetTexture.Release();
				Destroy( TargetTexture );
			}
		}

		private void LateUpdate()
		{

			Vector3 forward = _referenceTransform.forward;
			if( _prevForward != forward )
			{
				float xAbs = forward.x < 0 ? -forward.x : forward.x;
				float yAbs = forward.y < 0 ? -forward.y : forward.y;
				float zAbs = forward.z < 0 ? -forward.z : forward.z;

				GizmoComponent facingDirection;
				float facingDirectionCosine;
				if( xAbs > yAbs )
				{
					if( xAbs > zAbs )
					{
						facingDirection = forward.x > 0 ? GizmoComponent.XPositive : GizmoComponent.XNegative;
						facingDirectionCosine = Vector3.Dot( forward, new Vector3( 1f, 0f, 0f ) );
					}
					else
					{
						facingDirection = forward.z > 0 ? GizmoComponent.ZPositive : GizmoComponent.ZNegative;
						facingDirectionCosine = Vector3.Dot( forward, new Vector3( 0f, 0f, 1f ) );
					}
				}
				else if( yAbs > zAbs )
				{
					facingDirection = forward.y > 0 ? GizmoComponent.YPositive : GizmoComponent.YNegative;
					facingDirectionCosine = Vector3.Dot( forward, new Vector3( 0f, 1f, 0f ) );
				}
				else
				{
					facingDirection = forward.z > 0 ? GizmoComponent.ZPositive : GizmoComponent.ZNegative;
					facingDirectionCosine = Vector3.Dot( forward, new Vector3( 0f, 0f, 1f ) );
				}

				if( facingDirectionCosine < 0 )
					facingDirectionCosine = -facingDirectionCosine;

				if( facingDirectionCosine >= 0.92f ) // cos(20)
					SetHiddenComponent( GetOppositeComponent( facingDirection ) );
				else
					SetHiddenComponent( GizmoComponent.None );

				Quaternion rotation = _referenceTransform.rotation;
				_gizmoCamParent.localRotation = rotation;

				// Adjust the labels
				float xLabelPos = ( xAbs - 0.15f ) * 0.65f;
				float yLabelPos = ( yAbs - 0.15f ) * 0.65f;
				float zLabelPos = ( zAbs - 0.15f ) * 0.65f;

				if( xLabelPos < 0f )
					xLabelPos = 0f;
				if( yLabelPos < 0f )
					yLabelPos = 0f;
				if( zLabelPos < 0f )
					zLabelPos = 0f;

				_labelTransforms[0].localPosition = new Vector3( 0f, 0f, xLabelPos );
				_labelTransforms[1].localPosition = new Vector3( 0f, 0f, yLabelPos );
				_labelTransforms[2].localPosition = new Vector3( 0f, 0f, zLabelPos );

				_labelTransforms[0].rotation = rotation;
				_labelTransforms[1].rotation = rotation;
				_labelTransforms[2].rotation = rotation;

				_updateTargetTexture = true;
				_prevForward = forward;
			}

			if( _fadeT < 1f )
			{
				_fadeT += Time.unscaledDeltaTime * 4f;
				if( _fadeT >= 1f )
					_fadeT = 1f;

				SetAlphaOf( _fadingComponent, _isFadingToZero ? 1f - _fadeT : _fadeT );
				if( _fadeT >= 1f )
				{
					if( !_isFadingToZero )
					{
						SetMaterialOf( _fadingComponent, _gizmoDefaultMaterial );
						_fadingComponent = GizmoComponent.None;
					}
					else
					{
						_gizmoComponents[(int) _fadingComponent].gameObject.SetActive( false );
						_gizmoComponents[(int) GetOppositeComponent( _fadingComponent )].gameObject.SetActive( false );
					}
				}

				_updateTargetTexture = true;
			}

			if( _updateTargetTexture )
			{
				_gizmoCamera.Render();
				_updateTargetTexture = false;
			}
		}

		public GizmoComponent Raycast( Vector3 normalizedPosition )
		{
			RaycastHit hit;
			if( Physics.Raycast( _gizmoCamera.ViewportPointToRay( normalizedPosition ), out hit, _gizmoCamera.farClipPlane, 1 << GIZMOS_LAYER, QueryTriggerInteraction.Collide ) )
			{
				GameObject hitObject = hit.collider.transform.gameObject;
				for( int i = 0; i < _gizmoComponents.Length; i++ )
				{
					if( _gizmoComponents[i].gameObject == hitObject )
						return (GizmoComponent) i;
				}
			}
			return GizmoComponent.None;
		}

		public void OnPointerHover( Vector3 normalizedPosition )
		{
			// Set highlighted component
			GizmoComponent hitComponent = Raycast( normalizedPosition );
			if( hitComponent != GizmoComponent.None )
			{
				if( hitComponent != _highlightedComponent )
				{
					if( _highlightedComponent != GizmoComponent.None )
						_gizmoComponents[(int) _highlightedComponent].sharedMaterial = _gizmoDefaultMaterial;

					if( hitComponent != _fadingComponent )
					{
						_highlightedComponent = hitComponent;
						_gizmoComponents[(int) _highlightedComponent].sharedMaterial = _gizmoHighlightMaterial;
					}
					else
						_highlightedComponent = GizmoComponent.None;

					_updateTargetTexture = true;
				}
			}
			else if( _highlightedComponent != GizmoComponent.None )
			{
				_gizmoComponents[(int) _highlightedComponent].sharedMaterial = _gizmoDefaultMaterial;
				_highlightedComponent = GizmoComponent.None;

				_updateTargetTexture = true;
			}
		}

		private void SetHiddenComponent( GizmoComponent component )
		{
			if( component != GizmoComponent.None )
			{
				if( component != _fadingComponent )
				{
					if( _fadingComponent != GizmoComponent.None )
					{
						SetMaterialOf( _fadingComponent, _gizmoDefaultMaterial );
						SetAlphaOf( _fadingComponent, 1f );

						_gizmoComponents[(int) _fadingComponent].gameObject.SetActive( true );
						_gizmoComponents[(int) GetOppositeComponent( _fadingComponent )].gameObject.SetActive( true );
					}

					_fadingComponent = component;
					SetMaterialOf( _fadingComponent, _gizmoFadeMaterial );
					_isFadingToZero = true;
					_fadeT = 0f;
				}
			}
			else if( _fadingComponent != GizmoComponent.None && _fadeT >= 1f )
			{
				_gizmoComponents[(int) _fadingComponent].gameObject.SetActive( true );
				_gizmoComponents[(int) GetOppositeComponent( _fadingComponent )].gameObject.SetActive( true );

				_isFadingToZero = false;
				_fadeT = 0f;
			}
		}

		private void SetAlphaOf( GizmoComponent component, float alpha )
		{
			if( component == GizmoComponent.None ) return;

			_gizmoFadeMaterial.SetFloat( _gizmoMaterialFadeProperty, alpha );
			if( component == GizmoComponent.XNegative || component == GizmoComponent.XPositive )
				_labels[0].color = new Color( 1f, 1f, 1f, alpha );
			else if( component == GizmoComponent.ZNegative || component == GizmoComponent.ZPositive )
				_labels[2].color = new Color( 1f, 1f, 1f, alpha );
			else
				_labels[1].color = new Color( 1f, 1f, 1f, alpha );
		}

		private void SetMaterialOf( GizmoComponent component, Material material )
		{
			if( component == GizmoComponent.None ) return;

			GizmoComponent oppositeComponent = GetOppositeComponent( component );
			if( component == _highlightedComponent || oppositeComponent == _highlightedComponent ) _highlightedComponent = GizmoComponent.None;

			_gizmoComponents[(int) component].sharedMaterial = material;
			_gizmoComponents[(int) oppositeComponent].sharedMaterial = material;
		}

		private GizmoComponent GetOppositeComponent( GizmoComponent component )
		{
			if( component == GizmoComponent.None || component == GizmoComponent.Center ) return component;

			int val = (int) component;
			if( val % 2 == 0 ) return (GizmoComponent) ( val - 1 );

			return (GizmoComponent) ( val + 1 );
		}
    }

    public enum GizmoComponent { None = -1, Center = 0, XNegative = 1, XPositive = 2, YNegative = 3, YPositive = 4, ZNegative = 5, ZPositive = 6 };
}