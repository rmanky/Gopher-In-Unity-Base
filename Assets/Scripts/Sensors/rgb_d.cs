using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class rgb_d : MonoBehaviour
{

    public GameObject input_cam;
	public GameObject depth_render_cam;
	public bool alwaysOn = true;
    public bool visualization = false;

	public int samples = 10;
    public float angleMin = 0;
    public float angleMax = 180;
    public float angleIncrement;
    public int updateRate = 10;
    public float scanTime;

	public float cam_fieldOfView = 100.0f;

	public UnityEngine.Camera in_cam;
	public UnityEngine.Camera dr_cam;

	public int input_cam_res_width;
	public int input_cam_res_height;

    public RaycastHit[] raycastHits;
    public float[] directions;
	public List<float> ranges;
	public List<float> new_ranges;
	public List<Vector3> pixels;

    void Start()
    {
		raycastHits = new RaycastHit[samples];
		directions = new float[samples];

		// Calculate resolution based on angle limit and number of samples
		angleIncrement = (angleMax - angleMin)/(samples-1);
		
		input_cam = new GameObject("input_cam");
		input_cam.transform.SetParent(gameObject.transform, false);
		in_cam = input_cam.AddComponent<Camera>();
		in_cam.fieldOfView = cam_fieldOfView;
    	in_cam.targetTexture = new RenderTexture(1920, 1080, 24);
		in_cam.targetDisplay = 0;

		input_cam_res_width = in_cam.targetTexture.width;
		input_cam_res_height = in_cam.targetTexture.height;
		
		depth_render_cam = new GameObject("depth_render_cam");
		depth_render_cam.transform.SetParent(gameObject.transform, false);
		dr_cam = depth_render_cam.AddComponent<Camera>();
		dr_cam.fieldOfView = cam_fieldOfView;
		dr_cam.targetTexture = new RenderTexture(1920, 1080, 24);
		dr_cam.targetDisplay = 1;

		for (int i = 0; i < samples; ++i){
			directions[i] = angleMin + i*angleIncrement;
		}

		scanTime = 1f/updateRate;
		if (alwaysOn){
			InvokeRepeating("get_depth_data", 1f, scanTime);
			}
    }

	public void get_depth_data()
    {
        ranges.Clear();
		pixels.Clear();
		new_ranges.Clear();

        // Cast rays towards diffent directions to find colliders
        for (int i = 0; i < samples; ++i)
        {
            Vector3 rotation = GetRayRotation(i) * input_cam.transform.forward;
			

			for(int j = 0; j < samples; ++j){
				Vector3 rotation_up = GetRayRotation_up(j, rotation) * input_cam.transform.up;
				
				if (Physics.Raycast(input_cam.transform.position, rotation_up, out raycastHits[j], Mathf.Infinity)); 
				{
					ranges.Add(raycastHits[j].distance);
					//Need to make sure the collider is Mesh Collider, otherwise it will return zero vector
					if(raycastHits[j].collider){
						Vector3 pixel = this.convert_to_pixel(raycastHits[j]);
						pixels.Add(pixel);	
					}
					if (visualization){
						Debug.DrawRay(input_cam.transform.position, ranges[j]*rotation_up, Color.red, scanTime);
					}		
				}	
			}
        }

		//filtering out the ranges which are lesser than 0
		for(int i = 0; i < ranges.Count; i++){
			if(ranges[i] > 0){
				new_ranges.Add(ranges[i]);
			}
		}
	
    }

	public Vector3 convert_to_pixel(RaycastHit hit){
		var point = hit.point;
		var pixel = input_cam.GetComponent<Camera>().WorldToScreenPoint(point);
		return pixel;
	}

    public Quaternion GetRayRotation(int sampleInd) 
    {
        float angle = (angleMin + (angleIncrement * sampleInd));
        return Quaternion.AngleAxis(angle, input_cam.transform.up);
    }

	public Quaternion GetRayRotation_up(int sampleInd, Vector3 rotation) 
    {
        float angle = (angleMin + (angleIncrement * sampleInd));
        return Quaternion.AngleAxis(angle, rotation);
    }

	public List<float> GetCurrentScanRanges() 
    {
        return ranges;
    }

	private Texture2D get_inCam_texture(int res_width, int res_height, UnityEngine.Camera cam)
    {
        Rect rect = new Rect(0, 0, res_width, res_height);
        RenderTexture renderTexture = new RenderTexture(res_width, res_height, 24);
        Texture2D texture_2d = new Texture2D(res_width, res_height, TextureFormat.RGBA32, false);
        cam.targetTexture = renderTexture;
        cam.Render();
 
        RenderTexture.active = renderTexture;
        texture_2d.ReadPixels(rect, 0, 0);
 
        cam.targetTexture = null;	
        RenderTexture.active = null;
 
        Destroy(renderTexture);
        renderTexture = null;

        return texture_2d;
    }

	private void generate_depth_map(){
		in_cam.gameObject.SetActive(true);

		if(in_cam.gameObject.activeInHierarchy){

			Color white = new Color(1,1,1);
			Color black = new Color(0,0,0);

			Texture2D snap = new Texture2D(input_cam_res_width, input_cam_res_height, TextureFormat.RGB24, false);
			in_cam.Render();
			RenderTexture.active = in_cam.targetTexture;
			snap.ReadPixels(new Rect(0,0, input_cam_res_width, input_cam_res_height), 0, 0);
			
			// setting all the pixels to white color
			for(int i = 0; i < in_cam.targetTexture.width; i++){
				for(int j = 0; j < in_cam.targetTexture.height; j++){
					snap.SetPixel(i,j,white);
				}
			}
			// Assigning assign colors according to the depth
			for(int i = 0; i < pixels.Count; i++){
				//getting color according to ranges
				Color pixel_color = this.assign_color(new_ranges[i]);
				snap.SetPixel((int)pixels[i].x, (int)pixels[i].y, pixel_color);
				snap.SetPixel((int)pixels[i].x+1, (int)pixels[i].y, pixel_color);
				snap.SetPixel((int)pixels[i].x+1, (int)pixels[i].y+1, pixel_color);
				snap.SetPixel((int)pixels[i].x, (int)pixels[i].y+1, pixel_color);

			}

			byte[] bytes = snap.EncodeToPNG();
			string filename = this.SnapName();
			System.IO.File.WriteAllBytes(filename, bytes);
			Debug.Log("depth map stored!");
		}
	}

	private string SnapName(){
		return string.Format("{0}/SnapShots/snap_{1}x{2}_{3}.png",
		Application.dataPath,
		input_cam_res_width,
		input_cam_res_height,
		System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
	}

	private Color assign_color(float range){
		float scale = 20.0f;
		float intensity_val = range/scale;
		Color pixel_color = new Color(intensity_val, intensity_val, intensity_val);
		return pixel_color;
	}	

	void Update(){
		this.get_depth_data();
		this.generate_depth_map();	
	}

}
