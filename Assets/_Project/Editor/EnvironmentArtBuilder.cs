using System;
using System.Collections.Generic;
using PersistentWorld.Combat;
using PersistentWorld.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace PersistentWorld.Editor
{
    public static class EnvironmentArtBuilder
    {
        private const string TextureFolder = "Assets/Cainos/Pixel Art Top Down - Basic/Texture/";
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static Tilemap ground;
        private static Transform scenery;
        private static readonly Dictionary<string, Tile> Tiles = new Dictionary<string, Tile>();

        public static void Create(string sceneName)
        {
            Sprites.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder.TrimEnd('/') }))
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    if (asset is Sprite sprite) Sprites[sprite.name] = sprite;

            if (!Sprites.ContainsKey("TX Tileset Grass 0"))
                throw new InvalidOperationException("Import Cainos Pixel Art Top Down - Basic textures before rebuilding scenes.");

            Transform root = new GameObject("Cainos Environment").transform;
            GameObject grid = new GameObject("Ground grid", typeof(Grid));
            grid.transform.SetParent(root);
            GameObject tilemap = new GameObject("Ground tiles", typeof(Tilemap), typeof(TilemapRenderer));
            tilemap.transform.SetParent(grid.transform);
            ground = tilemap.GetComponent<Tilemap>();
            tilemap.GetComponent<TilemapRenderer>().sortingOrder = -10000;
            if (!AssetDatabase.IsValidFolder("Assets/_Project/EnvironmentTiles"))
                AssetDatabase.CreateFolder("Assets/_Project", "EnvironmentTiles");
            Tiles.Clear();
            scenery = new GameObject("Scenery").transform;
            scenery.SetParent(root);
            bool arena = sceneName == "BossArena";
            bool town = sceneName == "Town";

            // Extend grass past the collision boundary so the follow camera never reveals empty space.
            for (int y = -14; y <= 14; y++)
            for (int x = -22; x <= 22; x++)
            {
                int hash = ((x + 30) * 73 + (y + 30) * 37) % 16;
                Paint("TX Tileset Grass " + hash, x, y);
                bool plaza = arena ? Mathf.Abs(x) <= 7 && Mathf.Abs(y) <= 4
                    : town && x >= -4 && x <= 4 && y >= -3 && y <= 2;
                bool path = town ? y >= -1 && y <= 0 : Mathf.Abs(y - .5f * x) < 1.2f;
                if (plaza)
                {
                    int minX = arena ? -7 : -4;
                    int maxX = arena ? 7 : 4;
                    int minY = arena ? -4 : -3;
                    int maxY = arena ? 4 : 2;
                    int column = x == minX ? 0 : x == maxX ? 2 : 1;
                    int row = y == maxY ? 0 : y == minY ? 2 : 1;
                    Paint("TX Tileset Stone Ground_" + (row * 8 + column), x, y);
                }
                else if (path && Mathf.Abs(x) < 10 && Mathf.Abs(y) < 6)
                    Paint("TX Tileset Grass Pavement " + hash % 8, x, y);
            }

            // Clear travel lanes, spawn points and gates are deliberately kept open.
            for (int i = -5; i <= 5; i++)
            {
                Tree(new Vector2(i * 2.4f, 6.5f + (i % 2) * .4f), i);
                Tree(new Vector2(i * 2.4f + 1f, -8f), i + 1);
            }
            foreach (Vector2 position in new[] { new Vector2(-8, 2), new Vector2(-5, 4), new Vector2(6, -4), new Vector2(8, 2), new Vector2(-9,-2) })
                Tree(position, Mathf.RoundToInt(position.x));

            if (town)
            {
                Prop("TX Props Well", new Vector2(-2.8f, 1.5f));
                Prop("TX Props Stone Bench S", new Vector2(0, 2.8f));
                Prop("TX Props Barrel", new Vector2(-4, -2.5f));
                Prop("TX Props Crate", new Vector2(-4.8f, -2.4f));
                Prop("TX Props Road Sign E", new Vector2(6, 1.8f), false);
                Prop("TX Props Stone Lantern", new Vector2(4.5f, -2.8f));
            }
            else if (arena)
            {
                foreach (float x in new[] { -6f, 6f })
                foreach (float y in new[] { -3.5f, 3.5f })
                    Prop("TX Props Pillar", new Vector2(x, y));
                Prop("TX Props Altar", new Vector2(2, 3.8f));
                Prop("TX Props Statue", new Vector2(0, 4.5f));
            }
            else
            {
                Prop("TX Props Chest", new Vector2(5.5f, -3));
                Prop("TX Props Pillar Broken", new Vector2(4.8f, 3));
                Prop("TX Props - Stone 03", new Vector2(-3, -3));
                Prop("TX Props - Stone 05", new Vector2(6, .8f));
            }

            for (int i = 0; i < 38; i++)
            {
                float x = ((i * 47) % 180) / 10f - 9;
                float y = ((i * 31) % 100) / 10f - 5;
                if (Mathf.Abs(y) < 1.5f || (town && Mathf.Abs(x) < 5 && Mathf.Abs(y) < 3.5f) || arena) continue;
                Prop("TX Plant - Grass " + (i % 15 + 1).ToString("00"), new Vector2(x, y), false);
            }
        }

        private static void Paint(string name, int x, int y)
        {
            if (!Tiles.TryGetValue(name, out Tile tile))
            {
                string path = "Assets/_Project/EnvironmentTiles/" + name + ".asset";
                tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, path);
                }
                tile.sprite = Sprites[name];
                tile.colliderType = Tile.ColliderType.None;
                EditorUtility.SetDirty(tile);
                Tiles[name] = tile;
            }
            ground.SetTile(new Vector3Int(x, y, 0), tile);
        }

        private static SpriteRenderer Draw(string name, Vector2 position, Transform parent, int order)
        {
            if (!Sprites.TryGetValue(name, out Sprite sprite))
                throw new InvalidOperationException("Missing environment sprite: " + name);
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent);
            obj.transform.position = position;
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static void Tree(Vector2 position, int variant)
        {
            int type = Math.Abs(variant % 3) + 1;
            GameObject tree = new GameObject("Tree " + type);
            tree.transform.SetParent(scenery);
            tree.transform.position = position;
            SpriteRenderer lower = Draw("TX Tree T" + type + " Lower", position, tree.transform, 0);
            Draw("TX Tree T" + type + " Upper", position + Vector2.up * lower.sprite.rect.height / 32f, tree.transform, 0);
            CircleCollider2D trunk = tree.AddComponent<CircleCollider2D>();
            trunk.radius = .25f;
            tree.AddComponent<WorldObstacle>();
            tree.AddComponent<WorldDepthSort>();
        }

        private static void Prop(string name, Vector2 position, bool solid = true)
        {
            SpriteRenderer renderer = Draw(name, position, scenery, 0);
            renderer.gameObject.AddComponent<WorldDepthSort>();
            if (!solid) return;
            CircleCollider2D collider = renderer.gameObject.AddComponent<CircleCollider2D>();
            collider.radius = Mathf.Min(.45f, renderer.sprite.bounds.size.x * .3f);
            renderer.gameObject.AddComponent<WorldObstacle>();
        }
    }
}
