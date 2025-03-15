using UnityEngine;
using UnityEngine.InputSystem;

public class FPMovementScript : MonoBehaviour
{
    //MAKE SURE TO RESET THE SERIALIZED VARIABLES IN THE EDITOR IF YOU MAKE CHANGES HERE
    //TODO: swimming
    
    //---------------------------CONTROL BOOLEANS----------------------------------------------------------------------------------------------------
    [Header("Control Booleans")]
    public bool isWalking; public bool isSprinting; public bool isJumping; public bool isGrounded; public bool isMoving; public bool isCrouching;
    public bool isCameraFrozen;
    //---------------------------CONTROL BOOLEANS----------------------------------------------------------------------------------------------------
    
    //---------------------------MOVEMENT VARIABILES----------------------------------------------------------------------------------------------------
    [Header("Movement Speed")]
    [SerializeField] float maxSprintSpeed = 6.0f; [SerializeField] float maxBaseSpeed = 3.0f; [SerializeField] float maxWalkSpeed = 1.5f;
     [Header("Movement Acceleration")]
    [SerializeField] float sprintAcceleration = 3.0f; [SerializeField] float baseAcceleration = 1.5f; [SerializeField] float walkAcceleration = 0.5f;
     [Header("Movement Settings")]
    [SerializeField] float speedThreshold = 0.001f; [SerializeField] float maxGroundAngle = 60f; [SerializeField] float jumpStrength = 1f;
    private float currentSpeed; 
    private Vector3 moveDirection; 
    //---------------------------MOVEMENT VARIABILES----------------------------------------------------------------------------------------------------
    //---------------------------CROUCHING VARIABILES----------------------------------------------------------------------------------------------------
    [SerializeField] float crouchHeight; [SerializeField] float crouchCameraHeight = 1.0f; 
    [SerializeField] float crouchSpeed = 2.5f;
    float targetHeight; float targetCameraHeight;
    Vector3 originalCameraPosition; float baseCameraHeight; float baseHeight; 
    //---------------------------CROUCHING VARIABILES----------------------------------------------------------------------------------------------------

    //---------------------------RAYCASTING----------------------------------------------------------------------------------------------------
    float pivotOffsetGround = 0.8f;
    float rayDistanceVert = 0.5f; 
    Vector3 rayOrigin; Vector3 rayDirection; 
    //---------------------------RAYCASTING----------------------------------------------------------------------------------------------------

    //---------------------------INPUT ACTIONS----------------------------------------------------------------------------------------------------
    private PlayerInput playerInput;
    private InputAction moveAction; private InputAction sprintAction; private InputAction walkAction; private InputAction jumpAction; private InputAction crouchAction;
    private InputAction lookAction;
    //---------------------------INPUT ACTIONS----------------------------------------------------------------------------------------------------
    //---------------------------REQUIRED----------------------------------------------------------------------------------------------------
    private Rigidbody rigidBody;  public PAGravity gravitySystem;
    //---------------------------REQUIRED----------------------------------------------------------------------------------------------------

    //---------------------------MOUSE LOOK----------------------------------------------------------------------------------------------------
    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 100.0f; [SerializeField] public float maxVerticalAngle = 120.0f; private float xRotation = 0.0f;
    public Camera playerCamera; 
    //---------------------------MOUSE LOOK----------------------------------------------------------------------------------------------------    
    //---------------------------HEAD BOBBING----------------------------------------------------------------------------------------------------
    [Header("Head Bobbing")]
    [SerializeField] float bobFrequency = 5.0f; //how quickly the head bobs
    [SerializeField] float bobAmplitude = 0.001f; //intensity of bobbing
    [SerializeField] float sprintBobAmplitude = 0.002f; //intensity of bobbing when sprinting
    [SerializeField] float walkBobAmplitude = 0.0005f; //when walking
    private float bobTimer; //progress of bobbing cycle
    //---------------------------HEAD BOBBING----------------------------------------------------------------------------------------------------
    
    //---------------------------TRANSFORMS CACHE----------------------------------------------------------------------------------------------------
    private Transform playerTransform; private Transform cameraTransform;
    //---------------------------TRANSFORMS CACHE----------------------------------------------------------------------------------------------------



    private void Awake()
    {
        //Retrieve components attached to the game object. These are required for every player object. 
        rigidBody = GetComponent<Rigidbody>(); playerInput = GetComponent<PlayerInput>(); //changed character controller to rigid body for handling of gravity

        //Caching transforms to avoid constant instantiation and thus improving performance (very slightly!)
        playerTransform = transform; cameraTransform = playerCamera.transform;

        rigidBody.constraints = RigidbodyConstraints.FreezeRotation; //freezing rotation to avoid the player tripping over when moving around

        //Actions tied to input sets of the Input System. Configurable through Project Settings -> Input System Package
        moveAction = playerInput.actions["Move"]; sprintAction = playerInput.actions["Sprint"]; walkAction = playerInput.actions["Walk"];
        jumpAction = playerInput.actions["Jump"]; lookAction = playerInput.actions["Look"]; crouchAction = playerInput.actions["Crouch"];

        isCameraFrozen = false;
        currentSpeed = 0; //resets current speed to avoid weird artifacts or negative numbers
        originalCameraPosition = playerCamera.transform.localPosition;
        baseCameraHeight = playerCamera.transform.localPosition.y;
        //Debug.Log("Capsule height = " + baseHeight + " Camera height = " + baseCameraHeight);
    }

    private void OnEnable()
    {
        moveAction.Enable(); sprintAction.Enable(); walkAction.Enable(); jumpAction.Enable(); lookAction.Enable(); crouchAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable(); sprintAction.Disable(); walkAction.Disable(); jumpAction.Disable(); lookAction.Disable(); crouchAction.Disable();
    }

    private void Start()
    {
        gravitySystem = GetComponent<PAGravity>();
        if(gravitySystem == null)
        {
            Debug.Log("Gravity Component not assigned");
        }
        else { Debug.Log("Gravity: " + gravitySystem.isGravityEnabled); }
    }

    private void Update()
    {
        PlayerMovement();
        PlayerLook();
        PlayerJump();
        PlayerHeadBob();
        PlayerCrouch();
    }

    private void FixedUpdate()
    {
        CheckHorizontalSurface();
    }

    
    private void CheckHorizontalSurface()
    {        
        Vector3 offsetPositionGround = playerTransform.position - new Vector3(0, pivotOffsetGround, 0); //transform - offset
        Vector3 verRayDirection = Vector3.down * rayDistanceVert; // -1 * ray distance. Absolute coordinates cause we don't want players to walk on walls
        Ray horRay = new Ray(offsetPositionGround, verRayDirection);

        //Debug.DrawRay(offsetPositionGround, verRayDirection, Color.blue, 0.1f);
        
        if (Physics.Raycast(horRay, out RaycastHit hit, rayDistanceVert)) //if it hits something
        {
            if(Vector3.Angle(hit.normal, Vector3.up) <= maxGroundAngle) //if it's a normal within the max grounding angle
            {
                isGrounded = true; isJumping = false; //we're grounded.
            }
            else { isGrounded = false; } //we're not grounded
        }
        else { isGrounded = false; } //if it doesn't hit anything

        //Debug.Log("IsGrounded: " + isGrounded + ", hit normal: " + (hit.normal != Vector3.zero ? hit.normal.ToString() : "None"));
    }
    
    
    private void PlayerMovement()
    {
        Vector2 input = moveAction.ReadValue<Vector2>(); //read input from player
        moveDirection = (playerTransform.right * input.x) + (playerTransform.forward * input.y); //calculate direction based on input and orientation. Combines axis.

        //Get current movement state
        isSprinting = sprintAction.ReadValue<float>() > 0.5f; //IF THE INPUT IS HALF PRESSED(0.5), IT IS VALID INPUT
        isWalking = walkAction.ReadValue<float>() > 0.5f;
        isMoving = input.magnitude > 0.1f; //Check if there is any move input

        //Accelerate till max speed for selected state, with selected acceleration
        if(isMoving)
        {
            if(isSprinting) {currentSpeed +=  sprintAcceleration * Time.deltaTime; currentSpeed = Mathf.Clamp(currentSpeed, 0, maxSprintSpeed);}
            else if(isWalking || isCrouching) {currentSpeed +=  walkAcceleration * Time.deltaTime; currentSpeed = Mathf.Clamp(currentSpeed, 0, maxWalkSpeed);}
            else {currentSpeed +=  baseAcceleration * Time.deltaTime; currentSpeed = Mathf.Clamp(currentSpeed, 0, maxBaseSpeed);}
        }
        else { currentSpeed = Mathf.Lerp(currentSpeed, 0, 5 * Time.deltaTime); currentSpeed = Mathf.Clamp(currentSpeed, 0, maxBaseSpeed); } //deceleration when not pressing movement keys
        
        if(currentSpeed < speedThreshold) { currentSpeed = 0; } //normalizes speed back to zero to avoid numbers nearing zero with many digits

        //Actually perform the movement based on above multiplied by delta time. If it doesn't work try putting it on ifs above
        rigidBody.MovePosition(rigidBody.position + moveDirection * currentSpeed * Time.deltaTime); 
        //characterController.Move(moveDirection * currentSpeed * Time.deltaTime);

        //Debug.Log("Speed: " + currentSpeed);
    }

    private void PlayerLook()
    {
        if(isCameraFrozen) { return; } //skip the rest of the camera code if true

        Vector2 mouseInput = lookAction.ReadValue<Vector2>(); //read mouse input
        //calculate mouse movements
        float mouseX = mouseInput.x * mouseSensitivity * Time.deltaTime;
        float mouseY = mouseInput.y * mouseSensitivity * Time.deltaTime;

        //Update vertical rotation and clamp between max angles
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxVerticalAngle, maxVerticalAngle);

        playerTransform.Rotate(Vector3.up * mouseX); //rotate horizontally based on input
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0); //set local rotation of camera to mouse input
    }

    public void FreezeCamera(bool freeze)
    {
        isCameraFrozen = freeze;
    }

    private void PlayerJump()
    {
        
        if(jumpAction.ReadValue<float>() > 0.5f && isGrounded) //check if jump is triggered by pressing the right button and you're on the ground
        {
            rigidBody.AddForce(Vector3.up * jumpStrength, ForceMode.Impulse); //impulse helps us overcome gravity to perform jump.
            //not on the ground, is jumping
            isGrounded = false; isJumping = true;
        }
    }

    private void PlayerCrouch()
    {
        //Vector3.Lerp(starting vector, target vector, 0<float<1) --> interpolation between two vectors.    
        //Mathf.Lerp(starting float, target float, 0<float<1) --> interpolation between two numbers.   
        if(crouchAction.ReadValue<float>() > 0.5f && isGrounded) //if we're grounded and pressing the button
        {
            Debug.Log("Crouching...");
            isCrouching = true;
            //interpolate between standing position and crouching
            cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, 
                                                Mathf.Lerp(baseCameraHeight, crouchCameraHeight, crouchSpeed * Time.deltaTime),
                                                cameraTransform.localPosition.z);
        }
        else if (crouchAction.ReadValue<float>() < 0.5f && isCrouching) //if we're releasing the button and are crouching
        {
            Debug.Log("Not crouching...");
            isCrouching = false;
            //interpolate between crouching position and standing
            cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, 
                                                Mathf.Lerp(crouchCameraHeight, baseCameraHeight, crouchSpeed * Time.deltaTime),
                                                cameraTransform.localPosition.z);
        }
    }      



    private void PlayerHeadBob()
    {
        if(isMoving && !isJumping)
        {
            bobTimer += currentSpeed * bobFrequency * Time.deltaTime; //increment bobbing based on speed

            float bobMotion = Mathf.Sin(bobTimer); //use Sin function to create a waving motion
            //multiplies by the state's modifier to get desired effect
            if(isSprinting) {bobMotion *= sprintBobAmplitude;}
            else if(isWalking || isCrouching) {bobMotion *= walkBobAmplitude;}
            else {bobMotion *= bobAmplitude;}

            //Apply bobbingaa
            cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, cameraTransform.localPosition.y + bobMotion, cameraTransform.localPosition.z);
        }

        else
        {
            //Reset bobbing timer and camera when not moving or jumping
            bobTimer = 0;
            cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, originalCameraPosition.y, cameraTransform.localPosition.z);
        }
    }



}
