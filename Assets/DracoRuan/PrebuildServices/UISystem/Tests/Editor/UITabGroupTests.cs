using DracoRuan.PrebuildServices.UISystem.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    public class UITabGroupTests
    {
        private GameObject _go;
        private GameObject[] _tabGos;
        private GameObject[] _contentGos;

        [TearDown]
        public void TearDown()
        {
            if (this._go != null)
                Object.DestroyImmediate(this._go);
            if (this._tabGos != null)
                foreach (GameObject go in this._tabGos)
                    if (go != null)
                        Object.DestroyImmediate(go);
            if (this._contentGos != null)
                foreach (GameObject go in this._contentGos)
                    if (go != null)
                        Object.DestroyImmediate(go);
        }

        private UITabGroup NewTabGroup(int count, int initialIndex = 0)
        {
            this._go = new GameObject("TabGroup");
            var group = this._go.AddComponent<UITabGroup>();

            this._tabGos = new GameObject[count];
            this._contentGos = new GameObject[count];
            var tabs = new UITabGroup.Tab[count];

            for (int i = 0; i < count; i++)
            {
                this._tabGos[i] = new GameObject($"Tab{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                UIButton button = this._tabGos[i].AddComponent<UIButton>();
                button.ConfigureForTest(cooldownSeconds: 0f);

                this._contentGos[i] = new GameObject($"Content{i}");
                this._contentGos[i].SetActive(false);

                tabs[i] = new UITabGroup.Tab { button = button, content = this._contentGos[i] };
            }

            group.ConfigureForTest(tabs, initialIndex);
            return group;
        }

        [Test]
        public void ConfigureForTest_SelectsInitialIndex_AndActivatesOnlyItsContent()
        {
            UITabGroup group = this.NewTabGroup(3, initialIndex: 1);

            Assert.AreEqual(1, group.SelectedIndex.CurrentValue);
            Assert.IsFalse(this._contentGos[0].activeSelf);
            Assert.IsTrue(this._contentGos[1].activeSelf);
            Assert.IsFalse(this._contentGos[2].activeSelf);
        }

        [Test]
        public void Select_ActivatesOnlyChosenContent_DeactivatesOthers()
        {
            UITabGroup group = this.NewTabGroup(3);

            group.Select(2);

            Assert.AreEqual(2, group.SelectedIndex.CurrentValue);
            Assert.IsFalse(this._contentGos[0].activeSelf);
            Assert.IsFalse(this._contentGos[1].activeSelf);
            Assert.IsTrue(this._contentGos[2].activeSelf);
        }

        [Test]
        public void Select_OutOfRangeIndex_IsIgnored()
        {
            UITabGroup group = this.NewTabGroup(2);

            group.Select(99);

            Assert.AreEqual(0, group.SelectedIndex.CurrentValue);
        }

        [Test]
        public void ClickingTabButton_SelectsThatTab()
        {
            UITabGroup group = this.NewTabGroup(3);

            this._tabGos[2].GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(2, group.SelectedIndex.CurrentValue);
        }

        [Test]
        public void SelectNext_WrapsAroundToFirstTab()
        {
            UITabGroup group = this.NewTabGroup(3, initialIndex: 2);

            group.SelectNext();

            Assert.AreEqual(0, group.SelectedIndex.CurrentValue);
        }

        [Test]
        public void SelectPrevious_WrapsAroundToLastTab()
        {
            UITabGroup group = this.NewTabGroup(3, initialIndex: 0);

            group.SelectPrevious();

            Assert.AreEqual(2, group.SelectedIndex.CurrentValue);
        }
    }
}
