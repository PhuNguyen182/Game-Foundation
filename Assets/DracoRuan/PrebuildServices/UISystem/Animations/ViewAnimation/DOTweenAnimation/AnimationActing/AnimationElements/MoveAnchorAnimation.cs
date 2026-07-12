using System;
using DG.Tweening;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing.AnimationElements
{
    [CreateAssetMenu(fileName = "MoveAnchorAnimation", menuName = "DracoRuan/UISystem/AnimationConfig/MoveAnchorAnimation")]
    public class MoveAnchorAnimation : SingleAnimation
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
        
        [Header("Move Configs")]
        public AnchoredPositionData moveUpPositionData;
        public AnchoredPositionData moveDownPositionData;
        public AnchoredPositionData moveLeftPositionData;
        public AnchoredPositionData moveRightPositionData;
        
        [Header("Easing")]
        public AnimationEasing easing;
        
        private RectTransform _rectTransform;

        protected override void OnTargetSet()
        {
            base.OnTargetSet();
            this._rectTransform = this.Target.GetComponent<RectTransform>();
        }

        public override Tween BuildAnimation()
        {
            Sequence sequence = DOTween.Sequence();
            AnchoredPositionData movePositionData = this.GetMovePositionData();
            AnchoredPositionData oppositeMovePositionData = this.GetOppositeMovePositionData();
            this._rectTransform.anchorMin = oppositeMovePositionData.minAnchor;
            this._rectTransform.anchorMax = oppositeMovePositionData.maxAnchor;
            sequence.Insert(0, this._rectTransform.DOAnchorMin(movePositionData.minAnchor, this.duration));
            sequence.Insert(0, this._rectTransform.DOAnchorMax(movePositionData.maxAnchor, this.duration));

            switch (this.easing.easingType)
            {
                case AnimationEasingType.DOTweenEase:
                    sequence.SetEase(this.easing.ease);
                    break;
                case AnimationEasingType.AnimationCurve:
                    sequence.SetEase(this.easing.curve);
                    break;
            }

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
        
        private AnchoredPositionData GetOppositeMovePositionData()
        {
            AnchoredPositionData result = this.moveDirection switch
            {
                MoveDirection.Up => this.moveDownPositionData,
                MoveDirection.Down => this.moveUpPositionData,
                MoveDirection.Left => this.moveRightPositionData,
                MoveDirection.Right => this.moveLeftPositionData,
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
