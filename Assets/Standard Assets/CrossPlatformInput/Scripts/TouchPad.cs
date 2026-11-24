using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityStandardAssets.CrossPlatformInput
{
	[RequireComponent(typeof(Image))]
	public class TouchPad : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IInitializePotentialDragHandler
	{
		// Options for which axes to use
		public enum AxisOption
		{
			Both, // Use both
			OnlyHorizontal, // Only horizontal
			OnlyVertical // Only vertical
		}


		public enum ControlStyle
		{
			Absolute, // operates from teh center of the image
			Relative, // operates from the center of the initial touch
			Swipe, // swipe to touch touch no maintained center
		}


		public AxisOption axesToUse = AxisOption.Both; // The options for the axes that the still will use
		public ControlStyle controlStyle = ControlStyle.Absolute; // control style to use
		public string horizontalAxisName = "Horizontal"; // The name given to the horizontal axis for the cross platform input
		public string verticalAxisName = "Vertical"; // The name given to the vertical axis for the cross platform input
		public float Xsensitivity = 1f;
		public float Ysensitivity = 1f;

		Vector3 m_StartPos;
		Vector2 m_PreviousDelta;
		Vector3 m_JoytickOutput;
		bool m_UseX; // Toggle for using the x axis
		bool m_UseY; // Toggle for using the Y axis
		CrossPlatformInputManager.VirtualAxis m_HorizontalVirtualAxis; // Reference to the joystick in the cross platform input
		CrossPlatformInputManager.VirtualAxis m_VerticalVirtualAxis; // Reference to the joystick in the cross platform input
		Vector2 inputCenter;
        int lastDragFrame = -1;
        bool m_Dragging;
		int m_Id = -1;
		Vector2 m_PreviousTouchPos; // swipe style control touch


#if !UNITY_EDITOR
    private Vector3 m_Center;
    private Image m_Image;
#else
		Vector3 m_PreviousMouse;
#endif

		void OnEnable()
		{
			CreateVirtualAxes();
		}

        void Start()
        {
#if !UNITY_EDITOR
            m_Image = GetComponent<Image>();
            m_Center = m_Image.transform.position;
#endif
        }

		void CreateVirtualAxes()
		{
			// set axes to use
			m_UseX = (axesToUse == AxisOption.Both || axesToUse == AxisOption.OnlyHorizontal);
			m_UseY = (axesToUse == AxisOption.Both || axesToUse == AxisOption.OnlyVertical);

			// create new axes based on axes to use
			if (m_UseX)
			{
				m_HorizontalVirtualAxis = new CrossPlatformInputManager.VirtualAxis(horizontalAxisName);
				CrossPlatformInputManager.RegisterVirtualAxis(m_HorizontalVirtualAxis);
			}
			if (m_UseY)
			{
				m_VerticalVirtualAxis = new CrossPlatformInputManager.VirtualAxis(verticalAxisName);
				CrossPlatformInputManager.RegisterVirtualAxis(m_VerticalVirtualAxis);
			}
		}

		void UpdateVirtualAxes(Vector3 value)
		{
			value = value.normalized;
			if (m_UseX)
			{
				m_HorizontalVirtualAxis.Update(value.x);
			}

			if (m_UseY)
			{
				m_VerticalVirtualAxis.Update(value.y);
			}
		}


        public void OnPointerDown(PointerEventData data)
        {
            if (m_Dragging && data.pointerId != m_Id) return;
            m_Dragging = true; m_Id = data.pointerId;
            inputCenter = controlStyle == ControlStyle.Absolute ? (Vector2)transform.position : data.position;
            m_PreviousTouchPos = data.position;
            OnDrag(data);
        }

        public void OnInitializePotentialDrag(PointerEventData data) { data.useDragThreshold = false; }

        public void OnDrag(PointerEventData data)
        {
            if (!m_Dragging || data.pointerId != m_Id) return;
            Vector2 center = controlStyle == ControlStyle.Swipe ? m_PreviousTouchPos : inputCenter;
            Vector2 delta = (data.position - center).normalized;
            delta.x *= Xsensitivity; delta.y *= Ysensitivity;
            m_PreviousTouchPos = data.position; lastDragFrame = Time.frameCount;
            UpdateVirtualAxes(delta);
        }

        void LateUpdate()
        {
            if (m_Dragging && controlStyle == ControlStyle.Swipe && lastDragFrame != Time.frameCount)
                UpdateVirtualAxes(Vector3.zero);
        }
		public void OnPointerUp(PointerEventData data)
		{
            if (!m_Dragging || data.pointerId != m_Id) return;
			m_Dragging = false;
			m_Id = -1;
			UpdateVirtualAxes(Vector3.zero);
		}

		void OnDisable()
		{
			if (CrossPlatformInputManager.AxisExists(horizontalAxisName))
				CrossPlatformInputManager.UnRegisterVirtualAxis(horizontalAxisName);

			if (CrossPlatformInputManager.AxisExists(verticalAxisName))
				CrossPlatformInputManager.UnRegisterVirtualAxis(verticalAxisName);
		}
	}
}
