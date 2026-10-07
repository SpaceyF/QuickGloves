using System;
using System.Collections.Generic;
using BoneLib;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.AI;
using Il2CppSLZ.Marrow.Interaction;
using MelonLoader;
using UnityEngine;
using UnityEngine.XR;
using Hand = Il2CppSLZ.Marrow.Hand;

namespace QuickGloves
{
    // one glove. point, squeeze, yoink, catch.
    public class GloveHand
    {
        private enum State { Idle, Targeting, Tethered, Flight }

        private const float PressOn     = 0.6f;
        private const float PressOff    = 0.3f;
        private const float MinDist     = 0.7f;    // closer than this? just grab it lol
        private const float AimCone     = 20f;     // degrees of close enough
        private const float Sticky      = 7f;      // don't ditch your target so easily
        private const float ScanEvery   = 0.06f;
        private const float SettleTime  = 0.08f;   // chill a sec before a flick counts
        private const float StrainAccel = 18f;
        private const float BreakFree   = 0.35f;   // meters it moved, so it's free now
        private const int   KickSteps   = 4;       // hold the launch a few steps
        private const int   StuckSteps  = 10;
        private const float OwnRetry    = 0.3f;
        private const float WindMax     = 0.1f;    // longest we wait for your big flick
        private const float HomeRadius  = 1.22f;   // 4 ft, then it's on its own
        private const float HomeAccel   = 60f;     // how hard it chases you
        private const float HomeMaxTime = 6f;

        private readonly bool    _isLeft;
        private readonly GloveFx _fx;
        public GloveHand? Other;

        private State      _state;
        private Rigidbody? _rb;
        private int        _rbId;
        private bool       _grabLocked;

        // what your hand is doing
        private bool    _held, _pressed;
        private Vector3 _vel, _lastRel;
        private bool    _haveRel;

        // what you're pointing at
        private float _scanTimer, _pulseTimer;

        // it's on the leash
        private float   _tetherTime, _ownTimer, _humTimer;
        private bool    _strain;

        // how hard you yoinked
        private bool  _winding;
        private float _peak, _windTime, _power = 1f;
        private Vector3 _strainStart;

        // it's airborne
        private float   _t, _flightT, _launchSpeed, _drag;
        private Vector3 _kick, _gravity, _launchFrom;
        private int     _steps;
        private bool    _freed;
        private bool    _homing;   // still chasing your hand
        private float   _cruise;   // chase speed, m/s
        private readonly List<Rigidbody> _bodies = new List<Rigidbody>();

        private struct Cand { public float Score; public Collider Col; public Rigidbody Rb; }
        private readonly List<Cand> _cands = new List<Cand>();
        private readonly HashSet<int> _seen = new HashSet<int>();

        // remember who's pullable, save some work
        private struct Checked { public Rigidbody? Main; public float Until; }
        private static readonly Dictionary<int, Checked> _checked = new Dictionary<int, Checked>();

        public GloveHand(bool isLeft) { _isLeft = isLeft; _fx = new GloveFx(isLeft); }

        public int TargetId => _state == State.Idle ? 0 : _rbId;

        private Hand? MyHand() => _isLeft ? Player.LeftHand : Player.RightHand;

        // runs every frame
        public void Update()
        {
            Hand? hand = MyHand();
            BaseController? ctrl = _isLeft ? Player.LeftController : Player.RightController;
            if (hand == null || ctrl == null) { Cancel(null); return; }

            float dt = Time.deltaTime;
            Vector3 palm = Palm(hand);
            TrackVelocity(ctrl, dt);
            ReadInput(hand, ctrl);

            try
            {
                if (_state != State.Idle && !Alive()) Cancel(hand);

                switch (_state)
                {
                    case State.Idle:
                    case State.Targeting: Aim(hand, palm, dt);   break;
                    case State.Tethered:  Tether(hand, palm, dt); break;
                    case State.Flight:    Flight(hand, palm);     break;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[QuickGloves] " + e.Message);
                Cancel(hand);
            }
        }

        // runs every physics tick
        public void FixedTick()
        {
            if (_state == State.Idle || _state == State.Targeting) return;
            Hand? hand = MyHand();
            if (hand == null) return;
            try
            {
                if (!Alive()) return;
                if (_state == State.Flight) FlightTick(hand, Time.fixedDeltaTime);
                else if (_strain) StrainTick(hand);
            }
            catch { Cancel(hand); }
        }

        public void Reset()
        {
            _grabLocked = false;   // old hand got deleted, rip
            Cancel(null);
            _checked.Clear();
            _haveRel = false;
            _held = false;
        }

        public void Disable() => Cancel(MyHand());

        // read the squeeze
        private void ReadInput(Hand hand, BaseController ctrl)
        {
            float g;
            try { g = GloveSettings.UseTrigger ? hand.GetIndexTriggerAxis() : ctrl.GetGripForce(); }
            catch { g = 0f; }

            bool was = _held;
            if (!_held && g >= PressOn) _held = true;
            else if (_held && g <= PressOff) _held = false;
            _pressed = _held && !was;
        }

        // hand speed, minus you walking around
        private void TrackVelocity(BaseController ctrl, float dt)
        {
            try
            {
                Vector3 rel = ((Component)ctrl).transform.position - BodyPos();
                if (_haveRel && dt > 0.0001f)
                    _vel = Vector3.Lerp(_vel, (rel - _lastRel) / dt, 0.5f);
                _lastRel = rel;
                _haveRel = true;
            }
            catch { _vel = Vector3.zero; }
        }

        private static Vector3 BodyPos()
        {
            try { return Player.PhysicsRig.torso.rbPelvis.position; }
            catch { return Vector3.zero; }
        }

        // pointing at stuff
        private void Aim(Hand hand, Vector3 palm, float dt)
        {
            // fist, full hand, or something right there
            if ((_state == State.Idle && _held) || !HandEmpty(hand) || NearSomething(hand))
            {
                if (_state == State.Targeting) Cancel(hand);
                _fx.Crosshair(false, default, default, default, false);
                _fx.Beam(false, default, default, 0f, 0f, default);
                return;
            }

            Vector3 dir = ((Component)hand).transform.forward;

            _scanTimer -= dt;
            if (_scanTimer <= 0f)
            {
                _scanTimer = ScanEvery;
                Rigidbody? found = Scan(palm, dir);
                int id = found != null ? found.GetInstanceID() : 0;
                if (id != _rbId || found == null)
                {
                    _rb = found;
                    _rbId = id;
                    if (found != null)
                    {
                        Rumble(0.22f, 0.02f);
                        _pulseTimer = 0.4f;
                        GloveSound.Play(GloveSound.Target, _isLeft, palm, 0.25f);
                    }
                }
            }

            Color c = GloveFx.Accent();
            Vector3 eye = Eye(palm);
            float reach = AimReach(palm, dir);

            if (_rb == null)
            {
                if (_state == State.Targeting) Cancel(hand);
                _fx.Crosshair(true, palm + dir * reach, eye, c, false);
                _fx.Beam(true, palm, dir, reach, GloveSettings.AimRadius, c);
                return;
            }

            _state = State.Targeting;
            Lock(hand);

            // tiny buzz so you know it sees it
            _pulseTimer -= dt;
            if (_pulseTimer <= 0f) { _pulseTimer = 0.4f; Rumble(0.08f, 0.02f); }

            Vector3 at = _rb.worldCenterOfMass;
            _fx.Crosshair(true, at, eye, c, true);
            _fx.Beam(true, palm, dir, reach, GloveSettings.AimRadius, c);
            _fx.Arc(true, palm, at, c, 0.38f + 0.12f * Mathf.Sin(Time.time * 7f), false);
            _fx.Outline(_rb);
            _fx.OutlineTick(c, 0.10f);

            if (_pressed)
            {
                // double check, no stale answers
                _checked.Clear();
                if (Scan(palm, dir, _rbId) == null) { Cancel(hand); return; }

                _state      = State.Tethered;
                _tetherTime = 0f;
                _strain     = false;
                _winding    = false;
                _power      = 1f;
                _ownTimer   = 0f;
                _vel        = Vector3.zero;   // aiming doesn't count as a flick
                if (GloveManager.FusionAvailable) GloveFusion.TakeOwnership(_rb.gameObject);
                Rumble(0.4f, 0.03f);
                GloveSound.Play(GloveSound.Lock, _isLeft, palm, 0.7f);
            }
        }

        private static Vector3 Eye(Vector3 fallback)
        {
            try { if (Player.Head != null) return Player.Head.position; } catch { }
            return fallback;
        }

        // where your pointing actually lands
        private static float AimReach(Vector3 palm, Vector3 dir)
        {
            float range = GloveSettings.Range;
            try
            {
                if (Physics.Raycast(palm + dir * 0.15f, dir, out RaycastHit hit, range - 0.15f,
                                    ~0, QueryTriggerInteraction.Ignore))
                    return hit.distance + 0.15f;
            }
            catch { }
            return range;
        }

        private Rigidbody? Scan(Vector3 origin, Vector3 dir, int onlyId = 0)
        {
            RaycastHit[] hits;
            try
            {
                hits = Physics.SphereCastAll(origin, GloveSettings.AimRadius, dir, GloveSettings.Range,
                                             ~0, QueryTriggerInteraction.Ignore);
            }
            catch { return null; }

            _cands.Clear();
            _seen.Clear();
            int otherId = Other != null ? Other.TargetId : 0;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null) continue;
                Rigidbody rb = col.attachedRigidbody;
                if (rb == null) continue;
                if (!_seen.Add(rb.GetInstanceID())) continue;

                Vector3 to = rb.worldCenterOfMass - origin;
                float d = to.magnitude;
                if (d < MinDist || d > GloveSettings.Range) continue;

                float angle = Vector3.Angle(dir, to);
                if (angle > AimCone) continue;

                _cands.Add(new Cand { Score = angle + d * 0.2f, Col = col, Rb = rb });
            }

            Rigidbody? best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < _cands.Count; i++)
            {
                Cand c = _cands[i];
                Rigidbody? main = Validate(c.Col, c.Rb);
                if (main == null) continue;
                int id = main.GetInstanceID();
                if (id == otherId) continue;
                if (onlyId != 0 && id != onlyId) continue;

                float score = c.Score - (id == _rbId ? Sticky : 0f);
                if (score >= bestScore) continue;
                if (!Visible(origin, main)) continue;
                best = main;
                bestScore = score;
            }
            return best;
        }

        // hands back the prop's main chunk
        private static Rigidbody? Validate(Collider col, Rigidbody rb)
        {
            int key = rb.GetInstanceID();
            if (_checked.TryGetValue(key, out Checked ck) && ck.Until > Time.time) return ck.Main;

            Rigidbody? main = null;
            try { main = ValidateNow(col, rb); } catch { main = null; }
            _checked[key] = new Checked { Main = main, Until = Time.time + 0.75f };
            return main;
        }

        private static Rigidbody? ValidateNow(Collider col, Rigidbody rb)
        {
            GameObject go = col.gameObject;
            if (go.GetComponentInParent<RigManager>() != null) return null;   // no yoinking players
            if (go.GetComponentInParent<AIBrain>() != null) return null;      // no yoinking npcs either

            var host = go.GetComponentInParent<InteractableHost>();
            if (host == null) { if (go.GetComponentInParent<Grip>() == null) return null; }
            else if (host.IsStatic) return null;

            Rigidbody main = rb;
            GameObject root = rb.gameObject;
            var ent = go.GetComponentInParent<MarrowEntity>();
            if (ent != null)
            {
                root = ent.gameObject;
                var anchor = ent.AnchorBody;
                if (anchor != null && anchor._rigidbody != null) main = anchor._rigidbody;
            }

            if (main.isKinematic || main.mass > GloveSettings.MaxMass) return null;

            // somebody's holding it, hands off
            foreach (Grip g in root.GetComponentsInChildren<Grip>(false))
                if (g != null && g.HasAttachedHands()) return null;

            return main;
        }

        // no grabbing through walls (to lock on)
        private static bool Visible(Vector3 origin, Rigidbody rb)
        {
            try
            {
                Vector3 to = rb.worldCenterOfMass - origin;
                float d = to.magnitude;
                if (d < 0.2f) return true;
                Vector3 n = to / d;
                if (!Physics.Raycast(origin + n * 0.12f, n, out RaycastHit hit, d - 0.12f,
                                     ~0, QueryTriggerInteraction.Ignore)) return true;
                if (hit.rigidbody != null && hit.rigidbody.GetInstanceID() == rb.GetInstanceID()) return true;
                return hit.distance >= d - 0.6f;   // it just hit itself, that's fine
            }
            catch { return false; }
        }

        // holding the leash
        private void Tether(Hand hand, Vector3 palm, float dt)
        {
            Rigidbody rb = _rb!;
            if (!_held)
            {
                GloveSound.Play(GloveSound.Cancel, _isLeft, palm, 0.6f);
                Cancel(hand);
                return;
            }

            Vector3 at = rb.worldCenterOfMass;
            if (Vector3.Distance(palm, at) > GloveSettings.Range * 1.4f) { Cancel(hand); return; }

            _tetherTime += dt;
            Color c = GloveFx.Accent();
            _fx.Arc(true, palm, at, c, 0.9f, _strain);
            _fx.Crosshair(true, at, Eye(palm), c, true);
            _fx.Beam(false, default, default, 0f, 0f, c);
            _fx.Outline(rb);
            _fx.OutlineTick(c, _strain ? 0.3f : 0.22f);
            GloveSound.SetBeam(_isLeft, true, palm, _strain);

            if (_strain)
            {
                // it's stuck, make the glove groan
                _humTimer -= dt;
                if (_humTimer <= 0f) { _humTimer = 0.05f; Rumble(0.3f + 0.12f * Mathf.Sin(Time.time * 40f), 0.06f); }

                if (Vector3.Distance(at, _strainStart) > BreakFree) Launch(hand, palm);
                return;
            }

            if (_tetherTime < SettleTime) return;

            if (!_winding)
            {
                if (!Flicked(palm, at)) return;
                _winding  = true;
                _peak     = 0f;
                _windTime = 0f;
            }

            // wait for the top of your flick
            float speed = _vel.magnitude;
            _windTime += dt;
            if (GloveSettings.ForcePull && speed >= _peak && _windTime < WindMax) { _peak = speed; return; }
            _peak = Mathf.Max(_peak, speed);

            // wait for Fusion to hand it over
            if (GloveManager.FusionAvailable && !GloveFusion.CanMove(rb.gameObject))
            {
                _ownTimer -= dt;
                if (_ownTimer <= 0f) { _ownTimer = OwnRetry; GloveFusion.TakeOwnership(rb.gameObject); }
                return;
            }

            _power = ForceScale(_peak);
            Launch(hand, palm);
        }

        // flick power, just a little spice
        private static float ForceScale(float flick)
        {
            if (!GloveSettings.ForcePull) return 1f;
            float ratio = flick / Mathf.Max(0.1f, GloveSettings.FlickSpeed);
            return Mathf.Clamp(1f - 0.3f * GloveSettings.ForceSens * (ratio - 1.5f), 0.7f, 1.3f);
        }

        // back or up counts. shoving doesn't.
        private bool Flicked(Vector3 palm, Vector3 at)
        {
            float speed = _vel.magnitude;
            if (speed < GloveSettings.FlickSpeed) return false;

            Vector3 n = _vel / speed;
            Vector3 away = (palm - at).normalized;
            float back = Vector3.Dot(n, away);
            return back > 0.2f || (n.y > 0.5f && back > -0.2f);
        }

        private static Vector3 Gravity(Rigidbody rb) => rb.useGravity ? Physics.gravity : Vector3.zero;

        private static Vector3 CatchPoint(Vector3 palm) => palm + Vector3.up * 0.06f;

        // copying the physics engine's homework
        private static Vector3 StepVelocity(Vector3 v, Vector3 gravity, float drag, float dt)
            => (v + gravity * dt) * Mathf.Clamp01(1f - drag * dt);

        // how hard to throw so it lands
        private static Vector3 Solve(Vector3 from, Vector3 to, float time, Vector3 gravity, float drag)
        {
            float dt = Time.fixedDeltaTime;
            int steps = Mathf.Max(1, Mathf.RoundToInt(time / dt));
            float keep = Mathf.Clamp01(1f - drag * dt);

            float a = 1f, reach = 0f;   // how much throw is left
            Vector3 g = Vector3.zero, drop = Vector3.zero;
            for (int i = 0; i < steps; i++)
            {
                a *= keep;
                g = (g + gravity * dt) * keep;
                reach += a * dt;
                drop  += g * dt;
            }
            if (reach < 0.01f) reach = 0.01f;
            return (to - from - drop) / reach;
        }

        // grab every wobbly piece of the prop
        private void GatherBodies(Rigidbody rb)
        {
            _bodies.Clear();
            try
            {
                var ent = rb.GetComponentInParent<MarrowEntity>();
                if (ent != null)
                {
                    foreach (MarrowBody mb in ent.Bodies)
                    {
                        if (mb == null) continue;
                        Rigidbody part = mb._rigidbody;
                        if (part != null && !part.isKinematic) _bodies.Add(part);
                    }
                }
            }
            catch { }
            if (_bodies.Count == 0) _bodies.Add(rb);
        }

        private void SetVelocity(Vector3 v)
        {
            for (int i = 0; i < _bodies.Count; i++)
            {
                try { if (_bodies[i] != null) _bodies[i].velocity = v; } catch { }
            }
        }

        private void Launch(Hand hand, Vector3 palm)
        {
            Rigidbody rb = _rb!;
            Vector3 from = rb.worldCenterOfMass;
            Vector3 to   = CatchPoint(palm);
            float dist   = Vector3.Distance(from, to);

            // chonky things fly slower
            float heavy = Mathf.Clamp01(rb.mass / Mathf.Max(1f, GloveSettings.MaxMass));
            _flightT = Mathf.Clamp(0.38f + dist * 0.06f, 0.45f, 1.15f)
                     / Mathf.Max(0.3f, GloveSettings.FlightSpeed) * (1f + 0.3f * heavy) * _power;

            _gravity = Gravity(rb);
            _drag    = rb.drag;
            _kick    = Solve(from, to, _flightT, _gravity, _drag);

            GatherBodies(rb);
            rb.WakeUp();
            SetVelocity(_kick);
            rb.angularVelocity += UnityEngine.Random.insideUnitSphere * 2.5f;

            _launchFrom  = from;
            _launchSpeed = _kick.magnitude;
            _cruise      = Mathf.Max(3f, dist / _flightT);
            _t      = 0f;
            _steps  = 0;
            _freed  = false;
            _homing = GloveSettings.GravityPull;
            _strain = false;
            _state  = State.Flight;


            Rumble(1f, 0.04f);   // the pop!
            GloveSound.SetBeam(_isLeft, false, palm);
            GloveSound.Play(GloveSound.Pull, _isLeft, palm);
            _fx.Arc(false, default, default, default, 0f, false);
            _fx.Crosshair(false, default, default, default, false);
            _fx.Outline(null);
            _fx.TrailStart(from, GloveFx.Accent());
        }

        private void StrainTick(Hand hand)
        {
            Rigidbody rb = _rb!;
            Vector3 dir = CatchPoint(Palm(hand)) - rb.worldCenterOfMass;
            if (dir.sqrMagnitude < 0.0001f) return;
            rb.AddForce(dir.normalized * (rb.mass * StrainAccel), ForceMode.Force);
        }

        // wheee
        private void FlightTick(Hand hand, float dt)
        {
            Rigidbody rb = _rb!;
            _t += dt;
            _steps++;

            // this is the launch, held a bit.
            // floppy parts and the floor steal speed.
            // aim stays put, so no homing here.
            if (_steps <= KickSteps)
            {
                _kick = StepVelocity(_kick, _gravity, _drag, dt);
                SetVelocity(_kick);
                return;
            }

            if (_freed) { Home(hand, rb, dt); return; }

            // wait, did it even leave?
            if (_steps < StuckSteps) return;

            float moved = Vector3.Distance(rb.worldCenterOfMass, _launchFrom);
            if (moved >= _launchSpeed * _t * 0.3f) { _freed = true; return; }

            _fx.TrailStop();
            if (!_held) { Cancel(hand); return; }
            _state       = State.Tethered;
            _strain      = true;
            _strainStart = rb.worldCenterOfMass;
            _humTimer    = 0f;
        }

        // gravity pull: chase the glove like a puppy
        private void Home(Hand hand, Rigidbody rb, float dt)
        {
            if (!_homing) return;

            Vector3 from = rb.worldCenterOfMass;
            Vector3 to   = CatchPoint(Palm(hand));
            if (Vector3.Distance(from, to) <= HomeRadius || _t > HomeMaxTime)
            {
                // close enough, physics take the wheel
                _homing  = false;
                _flightT = Mathf.Max(_flightT, _t + 0.35f);
                return;
            }

            // steady speed, no slowing down to say hi
            float left = Mathf.Max(0.05f, Vector3.Distance(from, to) / _cruise);
            Vector3 want  = Solve(from, to, left, _gravity, _drag);
            Vector3 delta = Vector3.ClampMagnitude(want - rb.velocity, HomeAccel * dt);
            for (int i = 0; i < _bodies.Count; i++)
            {
                try { if (_bodies[i] != null) _bodies[i].velocity += delta; } catch { }
            }
        }

        private bool TimedOut() => _homing ? _t > HomeMaxTime + 0.5f : _t > _flightT + 0.45f;

        private void Flight(Hand hand, Vector3 palm)
        {
            Rigidbody rb = _rb!;
            _fx.TrailMove(rb.worldCenterOfMass);

            // manual mode: catch it yourself, champ
            if (!GloveSettings.HoldToCatch)
            {
                if (!_held) Unlock(hand);   // gotta let go first
                if (!HandEmpty(hand)) { Thump(rb.mass, palm); Cancel(hand); return; }
                if (TimedOut()) Cancel(hand);
                return;
            }

            if (TimedOut() || !HandEmpty(hand)) { Cancel(hand); return; }

            Vector3 surface;
            try { surface = rb.ClosestPointOnBounds(palm); } catch { surface = rb.worldCenterOfMass; }
            if (Vector3.Distance(palm, surface) > GloveSettings.CatchRadius) return;

            if (_held) Catch(hand, palm);
        }

        // heavy stuff thumps harder
        private void Thump(float mass, Vector3 palm)
        {
            float w = Mathf.Clamp01(mass / 8f);
            Rumble(0.5f + 0.5f * w, 0.05f + 0.1f * w);
            GloveSound.Play(GloveSound.Catch, _isLeft, palm, 0.45f + 0.25f * w);
        }

        private void Catch(Hand hand, Vector3 palm)
        {
            Rigidbody rb = _rb!;
            float mass = rb.mass;
            Grip? grip = PickGrip(rb, palm);

            // slow it down, no teleporting
            for (int i = 0; i < _bodies.Count; i++)
            {
                try
                {
                    if (_bodies[i] == null) continue;
                    _bodies[i].velocity        *= 0.25f;
                    _bodies[i].angularVelocity *= 0.25f;
                }
                catch { }
            }

            Cancel(hand);   // free up the hand first

            if (grip != null)
            {
                try
                {
                    if (GloveManager.FusionAvailable) GloveFusion.Attach(grip, hand);
                    else grip.OnGrabConfirm(hand, true);
                }
                catch (Exception e) { MelonLogger.Warning("[QuickGloves] catch: " + e.Message); }
            }

            Thump(mass, palm);
        }

        // guns show up trigger first
        private static Grip? PickGrip(Rigidbody rb, Vector3 palm)
        {
            try
            {
                var ent = rb.GetComponentInParent<MarrowEntity>();
                GameObject root = ent != null ? ent.gameObject : rb.gameObject;

                var gun = root.GetComponentInChildren<Gun>();
                if (gun != null && gun.triggerGrip != null) return gun.triggerGrip;

                Grip? best = null;
                float bestD = float.MaxValue;
                foreach (Grip g in root.GetComponentsInChildren<Grip>(false))
                {
                    if (g == null || g.IsInteractionDisabled || g.HasAttachedHands()) continue;
                    float d = (g.transform.position - palm).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = g; }
                }
                return best;
            }
            catch { return null; }
        }

        // little helper guys
        private bool Alive()
        {
            try { return _rb != null && _rb.gameObject != null && _rb.gameObject.activeInHierarchy; }
            catch { return false; }
        }

        private void Cancel(Hand? hand)
        {
            Unlock(hand);
            _state  = State.Idle;
            _rb     = null;
            _rbId   = 0;
            _strain = false;
            _winding = false;
            _scanTimer = 0f;
            _bodies.Clear();
            _fx.Hide();
            GloveSound.SetBeam(_isLeft, false, Vector3.zero);
        }

        // tells the game's own grab to back off
        private void Lock(Hand hand)
        {
            if (_grabLocked) return;
            try { hand.GrabLock = true; _grabLocked = true; } catch { }
        }

        private void Unlock(Hand? hand)
        {
            if (!_grabLocked) return;
            _grabLocked = false;
            try { if (hand != null) hand.GrabLock = false; } catch { }
        }

        private static Vector3 Palm(Hand hand)
        {
            try { var p = hand.palmPositionTransform; if (p != null) return p.position; } catch { }
            return ((Component)hand).transform.position;
        }

        private static bool HandEmpty(Hand hand)
        {
            try { return hand.m_CurrentAttachedGO == null; } catch { return true; }
        }

        private static bool NearSomething(Hand hand)
        {
            try { return hand.HoveringReceiver != null; } catch { return false; }
        }

        private void Rumble(float amp, float dur)
        {
            if (!GloveSettings.Haptics) return;
            try
            {
                var device = InputDevices.GetDeviceAtXRNode(_isLeft ? XRNode.LeftHand : XRNode.RightHand);
                if (device.isValid) device.SendHapticImpulse(0u, Mathf.Clamp01(amp * GloveSettings.HapticPower), dur);
            }
            catch { }
        }
    }
}
