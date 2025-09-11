export interface PetImageInfo {
    id: string;
    url: string;
    created_ts: number;
}


export interface MatchingPetInfo {
    id: number,
    name: string,
    owner_id: number,
    desc: string | null,
    profile_image_id: number | null,
    profile_image_url: string | null,
    images: [PetImageInfo],
}
