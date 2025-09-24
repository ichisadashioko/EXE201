export interface PetImageInfo {
    id: string;
    url: string;
    created_ts: number;
}


export interface MatchingPetInfo {
    id: number,
    name: string,
    owner_id: number,
    description: string | null,
    profile_image_id: number | null,
    profile_image_url: string | null,
    images: [PetImageInfo],
}


// // Generic API response type
// export interface ApiResponse<T = any> {
//     success: boolean;
//     status_code?: number | null;
//     data: T | null;
//     message: string;
//     error?: any;
// }

// // User profile
// export interface ApiUserProfile {
//     id: number;
//     name: string | null;
//     pets: ApiPetInfo[];
//     [key: string]: any; // for extra fields
// }

// // Pet info
// export interface ApiPetInfo {
//     id: number;
//     name: string;
//     profile_image_url: string | null;
//     [key: string]: any;
// }

// // Match info
// export interface MatchInfo {
//     user_a: { id: number; name: string | null };
//     user_b: { id: number; name: string | null };
//     user_a_liked_pets: ApiPetInfo[];
//     user_b_liked_pets: ApiPetInfo[];
//     creation_time: number;
// }

// // API contract for all exported functions
// export interface ApiContract {
//     api_login_with_email: (email: string, password: string) => Promise<ApiResponse<{ access_token: string }>>;
//     storeAccessToken: (token: string) => void;
//     getAccessToken: () => string | null;
//     api_get_user_profile: (token: string) => Promise<ApiResponse<{ user: ApiUserProfile }>>;
//     api_update_display_name: (access_token: string, new_name: string) => Promise<{ success: boolean; data?: any; message?: string }>;
//     api_pets_matching: (token: string) => Promise<ApiResponse<{ pets: ApiPetInfo[] }>>;
//     api_matching_record_store_rating: (token: string, pet_id: number, rating: number) => Promise<ApiResponse<any>>;
//     api_get_matches: (token: string) => Promise<ApiResponse<{ matches: MatchInfo[] }>>;
//     api_get_pet_info: (token: string, pet_id: number) => Promise<ApiResponse<{ pet: ApiPetInfo }>>;
//     api_create_new_pet: (token: string, name: string) => Promise<ApiResponse<{
//         pet?: {
//             id: number;
//         },
//         message: string;
//     }>>;
// }
