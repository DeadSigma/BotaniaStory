using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities; 
using Vintagestory.API.MathTools;       
using BotaniaStory.entities;

namespace BotaniaStory.client
{
    public class GaiaMusicTrack : IMusicTrack
    {
        private ICoreClientAPI capi;
        private EntityGaiaGuardian boss;
        private bool isActive = false;
        private ILoadedSound sound;

        public string Name => "GaiaBossTrack";
        public float Priority => 99f;
        public float StartPriority => 99f;
        public AssetLocation Location => new AssetLocation("botaniastory", "sounds/gaia_music");
        public bool IsActive => isActive;
        public ILoadedSound Sound => sound;

        public string PositionString => "GaiaArena";

        public GaiaMusicTrack(EntityGaiaGuardian boss)
        {
            this.boss = boss;
        }

        public void Initialize(IAssetManager assetManager, ICoreClientAPI capi, IMusicEngine musicEngine)
        {
            this.capi = capi;
        }

        public void BeginPlay(TrackedPlayerProperties props)
        {
            if (capi == null) return;

            sound = capi.World.LoadSound(new SoundParams
            {
                Location = Location,
                ShouldLoop = true,
                DisposeOnFinish = true,
                Volume = 1f,
                SoundType = EnumSoundType.Music
            });

            sound?.Start();
        }

        public bool ContinuePlay(float dt, TrackedPlayerProperties props)
        {
            if (!isActive || boss == null || !boss.Alive)
            {
                return false;
            }

            if (sound != null && capi.World.Player?.Entity != null)
            {
                float dist = (float)boss.Pos.DistanceTo(capi.World.Player.Entity.Pos);
                float maxDist = 60f;

                if (dist > maxDist)
                {
                    sound.SetVolume(0f);
                }
                else
                {
                    float targetVol = 1f - (dist / maxDist);
                    sound.SetVolume(targetVol);
                }
            }

            return true;
        }

        public bool ShouldPlay(TrackedPlayerProperties props, ClimateCondition conds, BlockPos pos)
        {
            return isActive;
        }

        public void FadeOut(float seconds, Action onFadedOut)
        {
            if (sound != null)
            {
                sound.FadeOutAndStop(seconds);
                capi.Event.RegisterCallback(t => onFadedOut?.Invoke(), (int)(seconds * 1000));
            }
            else
            {
                onFadedOut?.Invoke();
            }
        }

        public void UpdateVolume() { }
        public void FastForward(float seconds) { }
        public void BeginSort() { }

        public void Play() => isActive = true;
        public void Stop() => isActive = false;
    }
}