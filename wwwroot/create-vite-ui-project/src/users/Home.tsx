import { useEffect, useState } from "react"
import api_get_user_profile, { getAccessToken } from "../authentication";
import { useNavigate } from "react-router";


// Define the interface for a single Pet
interface Pet {
    id: number;
    name: string;
    description: string;
    profile_image_id: number | null;
    profile_image_url: string | null;
    created_at: string; // Dates are typically strings in JSON
}

// Define the interface for the User Profile, which contains an array of Pets
interface UserProfile {
    id: number;
    is_guest: boolean;
    created_at: string; // Dates are typically strings in JSON
    pets: Pet[];
}

// .Select(obj => new
// {
//     id = obj.Id,
//     is_guest = obj.IsGuest,
//     created_at = obj.CreatedAt,
//     pets = obj.Pets.Select(pet => new
//     {
//         id = pet.PetId,
//         name = pet.Name,
//         description = pet.Description,
//         profile_image_id = pet.ProfilePictureId,
//         profile_image_url = ((pet.ProfilePicture == null) ? null : pet.ProfilePicture.Url),
//         //species = pet.Species,
//         //breed = pet.Breed,
//         //age = pet.Age,
//         //bio = pet.Bio,
//         created_at = pet.CreatedAt,
//     }).ToList(),
// })

// This component now takes the list of pets as a prop and renders them
function PetList({ pets }: { pets: Pet[] }) {
    const navigate = useNavigate();

    if (pets.length === 0) {
        return (
            <div>
                <h2>Your Pets</h2>
                <p>You haven't created any pets yet.</p>
            </div>
        );
    }

    return (
        <div>
            <h2>Your Pets</h2>
            <ul>
                {pets.map(pet => (
                    <li
                        key={pet.id}
                        onClick={() => navigate(`/pets/${pet.id}`)}
                        style={{ cursor: 'pointer', marginBottom: '10px', listStyle: 'none' }}
                    >
                        {pet.profile_image_url && (
                            <img
                                src={pet.profile_image_url}
                                alt={pet.name}
                                style={{ width: '50px', height: '50px', marginRight: '10px', verticalAlign: 'middle', borderRadius: '5px' }}
                            />
                        )}
                        <span>{pet.name}</span>
                    </li>
                ))}
            </ul>
        </div>
    );
}

export default function Home() {
    // TODO fetch user profile and display user name
    // const [user, setUser] =
    // let [tmp_user_profile_json_obj, set_tmp_user_profile_json_obj] = useState<any>(null);

    const [user_profile, set_user_profile] = useState<UserProfile | null>(null);
    const navigate = useNavigate();
    const access_token = getAccessToken();

    useEffect(() => {
        if (access_token == null) {
            console.log("Access token is null, redirecting to login page");
            navigate("/login");
            return; // Return early
        }

        const load_data = async () => {
            try {
                const user_profile_response = await api_get_user_profile(access_token);
                console.debug(user_profile_response);
                if (user_profile_response.success) {
                    // Assuming the API response structure is { success: true, data: { user: { ... } } }
                    const user_profile_data = user_profile_response.data.user as UserProfile;
                    set_user_profile(user_profile_data);
                } else {
                    console.error("Failed to fetch user profile:", user_profile_response.message);
                    alert(`Failed to fetch user profile: ${user_profile_response.message}`);
                    navigate("/login");
                }
            } catch (error) {
                console.error("An error occurred while fetching user profile:", error);
                alert("An error occurred. Please try logging in again.");
                navigate("/login");
            }
        };

        load_data();

    }, [access_token, navigate]); // Add dependencies to prevent potential stale closures

    // Show a loading message while the user profile is being fetched
    if (!user_profile) {
        return <div>Loading your profile...</div>;
    }

    return (
        <div>
            <h1>Welcome, User #{user_profile.id}</h1>

            {/* Render the PetList component with the user's pets */}
            <PetList pets={user_profile.pets} />

            {/* Buttons for navigation */}
            <button onClick={() => navigate("/pets/create")}>Create New Pet</button>
            <button onClick={() => {
                console.log("matching clicked");
                navigate("/matching");
            }}>Matching</button>
        </div>
    )
}
