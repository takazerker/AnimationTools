using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AnimationTools;

public class AnimationEventEditorExample : AnimationEventEditor
{
    public override AnimationEvent DefaultAnimationEvent
    {
        get
        {
            var evt = new AnimationEvent();
            evt.functionName = "AnimEventFunc";
            return evt;
        }
    }
}
