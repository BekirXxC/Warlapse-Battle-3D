using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed partial class Battle3D
{
    VolumeProfile graphicsProfile;
    ColorAdjustments graphicsColor;
    UniversalAdditionalCameraData graphicsCamera;
    bool addedCameraData,previousPost,previousDither;
    LayerMask previousVolumeMask;
    AntialiasingMode previousAA;

    void EnableColorFinish()
    {
        graphicsCamera=view.GetComponent<UniversalAdditionalCameraData>();
        addedCameraData=!graphicsCamera;
        if(addedCameraData)graphicsCamera=view.gameObject.AddComponent<UniversalAdditionalCameraData>();
        previousPost=graphicsCamera.renderPostProcessing;
        previousVolumeMask=graphicsCamera.volumeLayerMask;
        previousAA=graphicsCamera.antialiasing;
        previousDither=graphicsCamera.dithering;
        graphicsCamera.renderPostProcessing=true;
        graphicsCamera.volumeLayerMask=1<<ReflectionLayer;
        graphicsCamera.antialiasing=AntialiasingMode.None;
        graphicsCamera.dithering=true;
        var holder=Joint(advancedRoot,"Işık ve renk dengesi",Vector3.zero);
        holder.gameObject.layer=ReflectionLayer;
        var volume=holder.gameObject.AddComponent<Volume>();
        volume.isGlobal=true;
        volume.priority=1000;
        volume.weight=1;
        graphicsProfile=ScriptableObject.CreateInstance<VolumeProfile>();
        volume.sharedProfile=graphicsProfile;
        graphicsColor=graphicsProfile.Add<ColorAdjustments>(true);
        graphicsColor.contrast.value=15;
        graphicsColor.postExposure.value=-.65f;
        graphicsColor.saturation.value=-2;
        var tone=graphicsProfile.Add<Tonemapping>(true);
        tone.mode.value=TonemappingMode.ACES;
        var bloom=graphicsProfile.Add<Bloom>(true);
        bloom.threshold.value=.86f;
        bloom.intensity.value=.16f;
        bloom.scatter.value=.45f;
        bloom.highQualityFiltering.value=false;
        var vignette=graphicsProfile.Add<Vignette>(true);
        vignette.intensity.value=.075f;
        vignette.smoothness.value=.65f;
    }

    void UpdateColorFinish()
    {
        if(!graphicsColor)return;
        float aboveGround=view.transform.position.y;
        float overview=Mathf.Max(Mathf.InverseLerp(4,26,aboveGround),Mathf.InverseLerp(.43f,.90f,-view.transform.forward.y));
        graphicsColor.postExposure.value=Mathf.Lerp(-.67f,.30f,overview)-(rainMesh ? .14f : 0);
        graphicsColor.contrast.value=Mathf.Lerp(16,7,overview);
    }

    void ReleaseColorFinish()
    {
        if(graphicsCamera)
        {
            graphicsCamera.renderPostProcessing=previousPost;
            graphicsCamera.volumeLayerMask=previousVolumeMask;
            graphicsCamera.antialiasing=previousAA;
            graphicsCamera.dithering=previousDither;
            if(addedCameraData)Destroy(graphicsCamera);
        }
        graphicsCamera=null;
        graphicsColor=null;
        addedCameraData=false;
        if(graphicsProfile)
        {
            foreach(var component in graphicsProfile.components)Destroy(component);
            graphicsProfile.components.Clear();
            Destroy(graphicsProfile);
        }
        graphicsProfile=null;
    }
}
