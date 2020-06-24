using UnityEngine;
using UnityEngine.UI;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Boo.Lang;

public class TableTennisAgent1 : Agent
{
    [Header("Specific to Tennis")]
    public GameObject myArea;
    public GameObject ball;
    //environmental settings
    public bool Opponent;
    public bool is2D = true;
    //observation
    public bool isSpinObservable = false;
    public bool isTiming = true;
    public bool isBallTouch = false;

    public float score;
    //正規化
    float max_Rx = 7;
    float max_Rv = 10;
    float max_Rw = 60;
    float max_Rf = 20;
    float max_Rt = 10F;
    float max_Bw = 1*2*Mathf.PI;

    Vector3 Restricted_Area = new Vector3(2, 2, 4);

    public Vector3 BallTouch;

    Rigidbody AgentRb;
    Rigidbody BallRb;

    Matrix4x4 Convert_x;
    Matrix4x4 Convert_w;
    Quaternion Convert_wq;
    float turn;

    // Looks for the scoreboard based on the name of the gameObjects.
    // Do not modify the names of the Score GameObjects

    public override void Initialize()
    {
        AgentRb = GetComponent<Rigidbody>();
        BallRb = ball.GetComponent<Rigidbody>();
        Convert_x = Matrix4x4.identity;
        Convert_x.m33 = 0;
        Convert_w = Matrix4x4.identity;
        Convert_w.m33 = 0;
        Convert_wq = Quaternion.AngleAxis(0, Vector3.up);
        if (Opponent)
        {
            Convert_x.m00 = -1;
            Convert_x.m22 = -1;
            Convert_w.m00 = -1;
            Convert_w.m22 = -1;
            Convert_wq = Quaternion.AngleAxis(180, Vector3.up);
        }
        if (is2D)
        {
            Convert_x.m00 = 0;
            Convert_w.m11 = 0;
            Convert_w.m22 = 0;
        }
        SetRacket();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        //racket position & velocity
        Vector3 racket_pos = AgentRb.position - myArea.transform.position;
        racket_pos = Convert_x * racket_pos;
        Vector3 racket_vel = Convert_x * AgentRb.velocity;
        Quaternion racket_rot = Convert_wq * transform.rotation;
        Vector3 racket_rotvel = Convert_w * AgentRb.angularVelocity;
        if (is2D)
        {
            racket_rot.y = 0;
            racket_rot.z = 0;
            racket_rotvel.y = 0;
            racket_rotvel.z = 0;
        }
        //ball position & velocity
        Vector3 ball_pos = BallRb.position - myArea.transform.position;
        ball_pos = Convert_x * ball_pos;
        Vector3 ball_vel = Convert_x * BallRb.velocity;
        Vector3 ball_rotvel = Convert_w * BallRb.angularVelocity;
        //normalize
        racket_pos = racket_pos / max_Rx;
        racket_vel = racket_vel / max_Rv;
        racket_rot = racket_rot.normalized;
        racket_rotvel = racket_rotvel / max_Rw;
        ball_pos = ball_pos / max_Rx;
        ball_vel = ball_vel / max_Rv;
        ball_rotvel = ball_rotvel / max_Bw;
        //racket 
        //position
        sensor.AddObservation(racket_pos);
        //velocity
        sensor.AddObservation(racket_vel);
        //quaternion
        sensor.AddObservation(racket_rot.x);
        sensor.AddObservation(racket_rot.y);
        sensor.AddObservation(racket_rot.z);
        sensor.AddObservation(racket_rot.w);
        //angular velocity
        sensor.AddObservation(racket_rotvel);
        //ball
        //position
        sensor.AddObservation(ball_pos);
        //velocity
        sensor.AddObservation(ball_vel);
        //spin
        if (isSpinObservable)
        {
            sensor.AddObservation(ball_rotvel);
        }
        if (isBallTouch)
        {
            sensor.AddObservation(BallTouch);
        }
        if (isTiming)
        {
            Rule rule = ball.GetComponent<Rule>();
            if (rule.log.hit == 0 && rule.log.bound == 1)
            {
                if (!Opponent && rule.log.turn == 1)
                    turn = 1F;
                else if (Opponent && rule.log.turn == 2)
                    turn = 1F;
                else
                    turn = 0F;
            }
            else
                turn = 0F;
            sensor.AddObservation(turn);
        }
    }

    public override void OnActionReceived(float[] vectorAction)
    {
        var force_x = Mathf.Clamp(vectorAction[0], -1, 1);
        var force_y = Mathf.Clamp(vectorAction[1], -1, 1);
        var force_z = Mathf.Clamp(vectorAction[2], -1, 1);
        var torque_x = Mathf.Clamp(vectorAction[3], -1, 1);
        var torque_y = Mathf.Clamp(vectorAction[4], -1, 1);
        var torque_z = Mathf.Clamp(vectorAction[5], -1, 1);
        Vector3 force = new Vector3(force_x, force_y, force_z) * max_Rf;
        force = Convert_x * limit_f(force, AgentRb.velocity, max_Rf);
        Vector3 torque = new Vector3(torque_x, torque_y, torque_z) * max_Rt;
        torque = Convert_w * limit_f(torque, AgentRb.angularVelocity, max_Rw);
        AgentRb.AddForce(force);
        AgentRb.AddTorque(torque);
    }

    private Vector3 limit_f(Vector3 force, Vector3 speed, float speed_limit)
    {
        Vector3 ret = force;
        if (speed.x * Mathf.Sign(force.x) > speed_limit)
            ret.x = 0F;
        if (speed.y * Mathf.Sign(force.y) > speed_limit)
            ret.y = 0F;
        if (speed.z * Mathf.Sign(force.z) > speed_limit)
            ret.z = 0F;
        return ret;
    }
    /**
    private void OnTriggerStay(Collider collision)
    {
        if (collision.gameObject.CompareTag("wall")
            || collision.gameObject.CompareTag("net")
            || collision.gameObject.CompareTag("racket_A")
            || collision.gameObject.CompareTag("racket_B")
            || collision.gameObject.CompareTag("court_A")
            || collision.gameObject.CompareTag("court_B"))
        {
            //AddReward(-0.01F);
        }
    }
    **/
    private void OnTriggerEnter(Collider collision)
    {
        if (collision.gameObject.CompareTag("ball"))
        {
            if (turn == 1)
                AddReward(0.3F);
            else
                AddReward(-0.1F);
        }
    }

    private void FixedUpdate()
    {
        Restrict_Area();
        BallTouch = Vector3.zero;
    }

    public override void Heuristic(float[] actionsOut)
    {
        actionsOut[2] = Input.GetAxis("Horizontal");    // Racket Movement
        actionsOut[1] = Input.GetAxis("Vertical");   // Racket Rotation
    }

    public override void OnEpisodeBegin()
    {
        SetRacket();
    }

    public void SetRacket()
    {
        Vector3 init_pos = Convert_x * new Vector3(Random.Range(0.1F, -0.1F), Random.Range(0.8F, 1.0F), Random.Range(-1.4F, -1.6F));
        AgentRb.position = init_pos + myArea.transform.position;
        AgentRb.velocity = Vector3.zero;
        Quaternion init_rot = Convert_wq * Quaternion.AngleAxis(60, Vector3.right);
        AgentRb.rotation = init_rot.normalized;
        AgentRb.angularVelocity = Vector3.zero;
    }
    public void Restrict_Area()
    {
        Vector3 area_pos = new Vector3(0, 1, -2);
        Vector3 area_size = new Vector3(4, 2, 4);
        Vector3 table_pos = new Vector3(0, 0, 0);
        Vector3 table_size = new Vector3(1.525F, 0.76F*2, 2.74F);
        in_the_box(AgentRb, area_pos, area_size);
        out_the_box(AgentRb, table_pos, table_size);
    }

    private void in_the_box(Rigidbody rb, Vector3 box_pos, Vector3 box_size)
    {
        Vector3 rb_pos = Convert_x * (rb.position - myArea.transform.position);
        Vector3 Re_v = Convert_x * rb.velocity;
        if ((rb_pos.x - box_pos.x) * Mathf.Sign(Re_v.x) > box_size.x / 2)
        {
            AddReward(-Mathf.Abs((Re_v.x+max_Rv*0.1F) / (max_Rv+max_Rv*0.1F)));
            Re_v.x = 0;
            rb_pos.x = (box_size.x / 2) * Mathf.Sign(rb_pos.x - box_pos.x) + box_pos.x;
        }
        if ((rb_pos.y - box_pos.y) * Mathf.Sign(Re_v.y) > box_size.y / 2)
        {
            AddReward(-Mathf.Abs((Re_v.y+max_Rv*0.1F) / (max_Rv+max_Rv*0.1F)));
            Re_v.y = 0;
            rb_pos.y = (box_size.y / 2) * Mathf.Sign(rb_pos.y - box_pos.y) + box_pos.y;
        }
        if ((rb_pos.z - box_pos.z) * Mathf.Sign(Re_v.z) > box_size.z / 2)
        {
            AddReward(-Mathf.Abs((Re_v.z+max_Rv*0.1F) / (max_Rv+max_Rv*0.1F)));
            Re_v.z = 0;
            rb_pos.z = (box_size.z / 2) * Mathf.Sign(rb_pos.z - box_pos.z) + box_pos.z;
        }
        rb.velocity = Convert_x * Re_v;
        rb_pos = Convert_x * rb_pos;
        rb.position = rb_pos + myArea.transform.position;
    }

    private void out_the_box(Rigidbody rb, Vector3 box_pos, Vector3 box_size)
    {
        Vector3 rb_pos = Convert_x * (rb.position - myArea.transform.position);
        Vector3 Re_v = Convert_x * rb.velocity;
        if (Mathf.Abs(rb_pos.x - box_pos.x) <= box_size.x / 2)
        {
            if (Mathf.Abs(rb_pos.y - box_pos.y) <= box_size.y / 2)
            {
                if (Mathf.Abs(rb_pos.z - box_pos.z) <= box_size.z / 2)
                {
                    Vector3 dis = rb_pos - (box_pos + box_size / 2);
                    Vector3 _dis = rb_pos - (box_pos - box_size / 2);
                    List<float> dis_list = new List<float> { Mathf.Abs(dis.x), Mathf.Abs(dis.y), Mathf.Abs(dis.z), Mathf.Abs(_dis.x), Mathf.Abs(_dis.y), Mathf.Abs(_dis.z) };
                    float min = dis_list[0];
                    int min_id = 0;
                    for(int i = 1; i < dis_list.Count; i++)
                    {
                        if(min > dis_list[i])
                        {
                            min = dis_list[i];
                            min_id = i;
                        }
                    }
                    if (min_id == 0)
                    {
                        rb_pos.x = box_pos.x + box_size.x / 2;
                        if (Re_v.x < 0)
                        {
                            AddReward(-Mathf.Abs((Re_v.x+1) / (max_Rv+1)));
                            Re_v.x = 0;
                        }
                    }
                    else if (min_id == 1)
                    {
                        rb_pos.y = box_pos.y + box_size.y / 2;
                        if (Re_v.y < 0)
                        {
                            AddReward(-Mathf.Abs((Re_v.y+max_Rv*0.1F) / (max_Rv+max_Rv*0.1F)));
                            Re_v.y = 0;
                        }
                    }
                    else if (min_id == 2)
                    {
                        rb_pos.z = box_pos.z + box_size.z / 2;
                        if (Re_v.z < 0)
                        {
                            AddReward(-Mathf.Abs((Re_v.z+max_Rv*0.1F) / (max_Rv+max_Rv*0.1F)));
                            Re_v.z = 0;
                        }
                    }
                    else if (min_id == 3)
                    {
                        rb_pos.x = box_pos.x - box_size.x / 2;
                        if (Re_v.x > 0)
                        {
                            AddReward(-Mathf.Abs((Re_v.x+max_Rv*0.1F) / (max_Rv+max_Rv*0.1F)));
                            Re_v.x = 0;
                        }
                    }
                    else if (min_id == 4)
                    {
                        if (Re_v.y > 0)
                        {
                            AddReward(-Mathf.Abs((Re_v.y+max_Rv*0.1F) / (max_Rv+max_Rv*0.1F)));
                            Re_v.y = 0;
                        }
                    }
                    else
                    {
                        rb_pos.z = box_pos.z - box_size.z / 2;
                        if (Re_v.z > 0)
                        {
                            AddReward(-Mathf.Abs((Re_v.z+max_Rv*0.1F) / (max_Rv+max_Rv*0.1F)));
                            Re_v.z = 0;
                        }
                    }

                }
            }
        } 
        rb.velocity = Convert_x * Re_v;
        rb_pos = Convert_x * rb_pos;
        rb.position = rb_pos + myArea.transform.position;
    }

}