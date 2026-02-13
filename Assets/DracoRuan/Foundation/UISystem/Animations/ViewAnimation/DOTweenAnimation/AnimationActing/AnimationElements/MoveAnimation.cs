using System;
using DG.Tweening;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing.AnimationElements
{
    [Serializable]
    public class MoveAnimation : AnimationElement
    {
        [Serializable]
        public enum MoveDirection
        {
            Up = 0,
            Down = 1,
            Left = 2,
            Right = 3,
        }
        
        public MoveDirection moveDirection;
        public AnchoredPositionData moveUpPositionData;
        public AnchoredPositionData moveDownPositionData;
        public AnchoredPositionData moveLeftPositionData;
        public AnchoredPositionData moveRightPositionData;
        public AnimationEasing easing;
        
        private RectTransform _rectTransform;
        
        public override void InitializeTarget(CanvasGroup target)
        {
            base.InitializeTarget(target);
            this._rectTransform = this.Target.GetComponent<RectTransform>();
        }

        public override Tween BuildAnimation()
        {
            Sequence sequence = DOTween.Sequence();
            AnchoredPositionData movePositionData = this.GetMovePositionData();
            sequence.Insert(0, this._rectTransform.DOAnchorMin(movePositionData.minAnchor, this.duration));
            sequence.Insert(0, this._rectTransform.DOAnchorMax(movePositionData.maxAnchor, this.duration));

            if (this.easing.easingType == AnimationEasingType.DOTweenEase)
                sequence.SetEase(this.easing.ease);
            else
                sequence.SetEase(this.easing.curve);

            return sequence;
        }

        private AnchoredPositionData GetMovePositionData()
        {
            AnchoredPositionData result = this.moveDirection switch
            {
                MoveDirection.Up => this.moveUpPositionData,
                MoveDirection.Down => this.moveDownPositionData,
                MoveDirection.Left => this.moveLeftPositionData,
                MoveDirection.Right => this.moveRightPositionData,
                _ => throw new ArgumentOutOfRangeException()
            };

            return result;
        }
    }
    
    [Serializable]
    public struct AnchoredPositionData
    {
        public Vector2 minAnchor;
        public Vector2 maxAnchor;
    }
}
