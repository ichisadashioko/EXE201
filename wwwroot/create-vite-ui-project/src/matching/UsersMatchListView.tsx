import { useNavigate } from "react-router";
import { getAccessToken } from "../authentication";

interface UserInfo {
    id: number,
    name: string | null,
}

interface PetInfo {
    id: number,
    name: string,
    owner_id: number,
    description: string | null,
    profile_image_id: number | null,
    profile_image_url: string | null,
}

interface MatchInfo {
    user_a: UserInfo,
    user_b: UserInfo,
    user_a_liked_pets: PetInfo[],
    user_b_liked_pets: PetInfo[],
    creation_time: number, // TODO unix timestamp
    // last_interaction_time: number, // TODO unix timestamp
}

interface UsersMatchListViewProps {
    me: UserInfo,
    matches: MatchInfo[],
}

// A helper component to display a pet's info
function PetDisplay({ pet }: { pet: PetInfo }) {
    return (
        <div style={{ textAlign: 'center', margin: '5px' }}>
            <img
                src={pet.profile_image_url || ''}
                alt={pet.name}
                style={{ width: '80px', height: '80px', borderRadius: '50%', objectFit: 'cover', border: '2px solid #0f0' }}
            />
            <p style={{ margin: '5px 0 0 0', fontSize: '0.9em' }}>{pet.name}</p>
        </div>
    );
}

export default function UsersMatchListView({ me, matches }: UsersMatchListViewProps) {
    if (!matches || matches.length === 0) {
        return (
            <div style={{ textAlign: 'center', padding: '40px', color: '#666' }}>
                <h2>No New Matches</h2>
                <p>When you and another user like each other's pets, you'll see them here.</p>
            </div>
        );
    }
    const navigate = useNavigate(); // Hook for navigation

    const handleMessageClick = async (otherUserId: number) => {
        try {
            const response = await fetch(`/api/chat/initiate/${otherUserId}`, {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${getAccessToken()}` }
            });
            if (response.ok) {
                const data = await response.json();
                navigate(`/chat/${data.chatThreadId}`); // Navigate to the chat view
            } else {
                console.error("Failed to initiate chat");
            }
        } catch (error) {
            console.error("Error initiating chat:", error);
        }
    };
    return (
        <div style={{ fontFamily: 'sans-serif', maxWidth: '700px', margin: 'auto' }}>
            <h1 style={{ textAlign: 'center' }}>Your Matches</h1>
            {matches.map((match) => {
                // Determine who the other user is
                const otherUser = (match.user_a.id === me.id) ? match.user_b : match.user_a;

                // Determine which list of pets belongs to the current user and which to the other user
                const pet_that_i_liked_list = (match.user_a.id === me.id) ? match.user_a_liked_pets : match.user_b_liked_pets;
                const my_pets_that_the_other_liked_list = (match.user_a.id === me.id) ? match.user_b_liked_pets : match.user_a_liked_pets;

                return (
                    <div key={`${match.user_a.id}-${match.user_b.id}`} style={{ border: '1px solid #ccc', borderRadius: '8px', padding: '16px', marginBottom: '16px', boxShadow: '0 2px 4px rgba(0,0,0,0.1)' }}>
                        <h2>You and {otherUser.name} have a match!</h2>
                        <p style={{ color: '#666', fontSize: '0.9em', marginTop: '-10px' }}>
                            Matched on: {new Date(match.creation_time * 1000).toLocaleDateString()}
                        </p>

                        <div style={{ display: 'flex', justifyContent: 'space-around', margin: '20px 0', background: '#f9f9f9', padding: '10px', borderRadius: '4px' }}>
                            <div>
                                <h4>They liked your pet(s):</h4>
                                <div style={{ display: 'flex', flexWrap: 'wrap' }}>
                                    {my_pets_that_the_other_liked_list.map(pet => <PetDisplay key={pet.id} pet={pet} />)}
                                </div>
                            </div>
                            <div>
                                <h4>You liked their pet(s):</h4>
                                <div style={{ display: 'flex', flexWrap: 'wrap' }}>
                                    {pet_that_i_liked_list.map(pet => <PetDisplay key={pet.id} pet={pet} />)}
                                </div>
                            </div>
                        </div>

                        <button
                            onClick={() => handleMessageClick(otherUser.id)}
                            style={{
                                width: '100%',
                                padding: '12px',
                                fontSize: '1.1em',
                                cursor: 'pointer',
                                background: '#007bff',
                                color: 'white',
                                border: 'none',
                                borderRadius: '4px'
                            }}>
                            Message {otherUser.name}
                        </button>
                    </div>
                );
            })}
        </div>
    );
}
