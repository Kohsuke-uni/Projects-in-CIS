using UnityEngine;

public class SpecialClearAnimationTester : MonoBehaviour
{
    public SpecialClearAnimationUI animationUI;

    void Update()
    {
        if (animationUI == null)
            return;

        if (Input.GetKeyDown(KeyCode.Alpha1))
            animationUI.PlayTetris();

        if (Input.GetKeyDown(KeyCode.Alpha2))
            animationUI.PlayTSpinDouble();

        if (Input.GetKeyDown(KeyCode.Alpha3))
            animationUI.PlayTSpinTriple();
    }
}
