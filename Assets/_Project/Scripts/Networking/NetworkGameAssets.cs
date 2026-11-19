using System.Collections.Generic;
using UnityEngine;
namespace PersistentWorld.Networking
{
    // 온라인에서 그림을 가리키는 목록. 텍스처마다 고유 번호(ids)를 붙여 보낸다.
    // 고유 번호는 그림의 프로젝트 경로로 만든 해시라서, 빌드한 PC마다 그림 개수·순서가 달라도 같은 그림은 같은 번호가 된다.
    // (예전처럼 목록의 몇 번째인지로 보내면, 누가 그림 하나만 더 가져도 뒤의 번호가 전부 밀린다.)
    public sealed class NetworkGameAssets : ScriptableObject
    {
        public Texture2D[] textures;
        public int[] ids;

        private Dictionary<int, Texture2D> byId;
        private Dictionary<Texture2D, int> byTexture;

        // 음수는 특수 그림(그림자·경고 원 등)에 쓰므로 고유 번호는 늘 16 이상
        public static int IdOf(string assetPath)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in assetPath.Replace('\\', '/').ToLowerInvariant()) { hash ^= c; hash *= 16777619; }
                int id = (int)(hash & 0x7FFFFFFF);
                return id < 16 ? id + 16 : id;
            }
        }

        public bool TryGet(int id, out Texture2D texture)
        {
            Index();
            return byId.TryGetValue(id, out texture) && texture != null;
        }

        public bool TryGetId(Texture2D texture, out int id)
        {
            Index();
            id = -1;
            return texture != null && byTexture.TryGetValue(texture, out id);
        }

        private void Index()
        {
            if (byId != null) return;
            byId = new Dictionary<int, Texture2D>();
            byTexture = new Dictionary<Texture2D, int>();
            if (textures == null) return;
            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] == null) continue;
                int id = ids != null && i < ids.Length ? ids[i] : i;
                byId[id] = textures[i];
                byTexture[textures[i]] = id;
            }
        }

        private void OnEnable() { byId = null; byTexture = null; }
    }
}
