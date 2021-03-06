﻿using UnityEngine;

/// <summary>
/// Objects that need to revalidate when their transform properties change.
/// </summary>
public interface IValidateOnTransformChange
{
    /// <summary>
    /// The on validate function must be publicly exposed.
    /// </summary>
    void OnValidate();
}

/// <summary>
/// Editor class that calls OnValidate when a transform is updated.
/// </summary>
[ExecuteInEditMode]
public class TransformValidator : MonoBehaviour
{
    #region Private Fields
    private Matrix4x4 priorMatrix = Matrix4x4.identity;
    #endregion
    #region Inspector Fields
    [Tooltip("Enable this when editing the transform to see live updates.")]
    [SerializeField] private bool liveUpdate = false;
    #endregion
    #region Editor Update Loop
    protected virtual void Update()
    {
        if (liveUpdate)
        {
            if (!transform.worldToLocalMatrix.Equals(priorMatrix))
            {
                priorMatrix = transform.worldToLocalMatrix;
                // Notify all scripts with editor features dependent
                // on the transform.
                foreach (IValidateOnTransformChange editorBehavior in
                    gameObject.GetComponents<IValidateOnTransformChange>())
                {
                    editorBehavior.OnValidate();
                }
            }
        }
    }
    #endregion
}
