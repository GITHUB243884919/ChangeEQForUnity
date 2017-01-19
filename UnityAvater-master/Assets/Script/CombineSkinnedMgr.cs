using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class UCombineSkinnedMgr 
{

    /// <summary>
    /// Only for merge materials.
    /// </summary>
	private const int COMBINE_TEXTURE_MAX = 512;
	private const string COMBINE_DIFFUSE_TEXTURE = "_MainTex";

    /// <summary>
    /// Combine SkinnedMeshRenderers together and share one skeleton.
    /// Merge materials will reduce the drawcalls, but it will increase the size of memory. 
    /// 
    /// skeleton是一个只带transfrom的prefab，很形象，就是一个骨架。
    /// meshes 是各个身体部件上的SkinnedMeshRenderer组件。
    /// 因为最终要合并，所以并不用把各部件的骨头设置父节点成骨架。
    /// 然而武器就不一样了，武器并不参与合并，所以还是要设置父节点。
    /// </summary>
    /// <param name="skeleton">combine meshes to this skeleton(a gameobject)</param>
    /// <param name="meshes">meshes need to be merged</param>
    /// <param name="combine">merge materials or not</param>
	public void CombineObject (GameObject skeleton, 
        SkinnedMeshRenderer[] meshes, bool combine = false)
    {

		// Fetch all bones of the skeleton
        // transforms是骨架上所有的transform组件的List
		List<Transform> transforms = new List<Transform>();
		transforms.AddRange(skeleton.GetComponentsInChildren<Transform>(true));

        // the list of materials
        // materials是所有身体部件上的所有Material组成的List
		List<Material> materials = new List<Material>();

        // the list of meshes
        // combineInstances是所有身体部件上所有的mesh组成的List
		List<CombineInstance> combineInstances = new List<CombineInstance>();

        //the list of bones
        List<Transform> bones = new List<Transform>();

		// Below informations only are used for merge materilas(bool combine = true)
		List<Vector2[]> oldUV = null;
		Material newMaterial = null;
		Texture2D newDiffuseTex = null;

		// Collect information from meshes
        // 这里分别把所有Material，Mesh，Transform(骨头)保存到对应的List
		for (int i = 0; i < meshes.Length; i ++)
		{
			SkinnedMeshRenderer smr = meshes[i];
            // Collect materials
			materials.AddRange(smr.materials); 

			// Collect meshes
			for (int sub = 0; sub < smr.sharedMesh.subMeshCount; sub++)
			{
				CombineInstance ci = new CombineInstance();
				ci.mesh = smr.sharedMesh;
				ci.subMeshIndex = sub;
				combineInstances.Add(ci);
			}

			// Collect bones
            // 收集骨头有点区别：只收集骨架中有的。这应该会涉及到具体的美术标准。
			for (int j = 0 ; j < smr.bones.Length; j ++)
			{
				int tBase = 0;
				for (tBase = 0; tBase < transforms.Count; tBase++)
				{
					if (smr.bones[j].name.Equals(transforms[tBase].name))
					{
                        //Debug.Log("Equals bones " + smr.bones[j].name);
						bones.Add(transforms[tBase]);
						break;
					}
				}
			}
		}

        // merge materials
        // Material合并，主要是处理贴图和其UV
		if (combine)
		{
			newMaterial = new Material (Shader.Find ("Mobile/Diffuse"));
			oldUV = new List<Vector2[]>();
			// merge the texture
            // 合并贴图（从收集的各Material中取）
            // Textures是所有贴图组成的列表
			List<Texture2D> Textures = new List<Texture2D>();
			for (int i = 0; i < materials.Count; i++)
			{
				Textures.Add(materials[i].GetTexture(COMBINE_DIFFUSE_TEXTURE) as Texture2D);
			}

            //所有贴图合并到newDiffuseTex这张大贴图上
			newDiffuseTex = new Texture2D(COMBINE_TEXTURE_MAX, COMBINE_TEXTURE_MAX, TextureFormat.RGBA32, true);
			Rect[] uvs = newDiffuseTex.PackTextures(Textures.ToArray(), 0);
			newMaterial.mainTexture = newDiffuseTex;

            // reset uv
            // 根据原来单个上的uv算出合并后的uv，uva是单个的，uvb是合并后的。
            // uva取自combineInstances[j].mesh.uv
            // 用oldUV保存uva。为什么要保存uva？它不是单个吗？先跳过往下看
            // 计算好uvb赋值到到combineInstances[j].mesh.uv
			Vector2[] uva, uvb;
			for (int j = 0; j < combineInstances.Count; j++)
			{
				//uva = (Vector2[])(combineInstances[j].mesh.uv);
                uva = combineInstances[j].mesh.uv;
				uvb = new Vector2[uva.Length];
				for (int k = 0; k < uva.Length; k++)
				{
					uvb[k] = new Vector2((uva[k].x * uvs[j].width) + uvs[j].x, (uva[k].y * uvs[j].height) + uvs[j].y);
				}
				//oldUV.Add(combineInstances[j].mesh.uv);
                oldUV.Add(uva);
				combineInstances[j].mesh.uv = uvb;
			}
		}

		// Create a new SkinnedMeshRenderer
		SkinnedMeshRenderer oldSKinned = 
            skeleton.GetComponent<SkinnedMeshRenderer> ();
		if (oldSKinned != null) 
        {
        	GameObject.DestroyImmediate (oldSKinned);
		}
		SkinnedMeshRenderer r = skeleton.AddComponent<SkinnedMeshRenderer>();
		r.sharedMesh = new Mesh();
        // Combine meshes
		r.sharedMesh.CombineMeshes(combineInstances.ToArray(), combine, false);
        // Use new bones
        r.bones = bones.ToArray();
		if (combine)
		{
            Debug.Log("combine " + combine);
			r.material = newMaterial;
            for (int i = 0; i < combineInstances.Count; i++)
            {
                // 这为什么要用oldUV，这不是保存的uva吗？它是单个的uv呀？
                // 原因在于，这行代码其实并不影响显示，影响显示的是在Mesh合并前的uv。
                // 这行的意义在于合并后，又要换部件时，在新的合并过程中找到正确的单个uv。
                // 也是oldUV存在的意义。
                combineInstances[i].mesh.uv = oldUV[i];
            }
		}
        else
		{
            Debug.Log("combine " + combine);
			r.materials = materials.ToArray();
		}
	}
}
