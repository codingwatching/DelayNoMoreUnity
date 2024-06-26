using UnityEngine;
using shared;
using System;
using UnityEngine.UI;

public class ParticipantSlot : MonoBehaviour {
    public Image underlyingImg;

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    private void toggleUnderlyingImage(bool val) {
        if (val) {
            underlyingImg.transform.localScale = Vector3.one;
        } else {
            underlyingImg.transform.localScale = Vector3.zero;
        }
    }

    public void SetAvatar(CharacterDownsync currCharacter) {
        if (null == currCharacter || Battle.TERMINATING_PLAYER_ID == currCharacter.Id) {
            toggleUnderlyingImage(false);
            return;
        }
        var chConfig = Battle.characters[currCharacter.SpeciesId];
        string speciesName = chConfig.SpeciesName;
        // Reference https://www.codeandweb.com/texturepacker/tutorials/using-spritesheets-with-unity#how-can-i-access-a-sprite-on-a-sprite-sheet-from-code
        string spriteSheetPath = String.Format("Characters/{0}/{0}", speciesName, speciesName);
        var sprites = Resources.LoadAll<Sprite>(spriteSheetPath);
        if (null == sprites || chConfig.UseIsolatedAvatar) {
            var sprite = Resources.Load<Sprite>(String.Format("Characters/{0}/Avatar_1", speciesName));
            if (null != sprite) {
                underlyingImg.sprite = sprite;
                toggleUnderlyingImage(true);
            }
        } else {
            foreach (Sprite sprite in sprites) {
                if ("Avatar_1".Equals(sprite.name)) {
                    underlyingImg.sprite = sprite;
                    toggleUnderlyingImage(true);
                    break;
                }
            }
        }
    }
}
