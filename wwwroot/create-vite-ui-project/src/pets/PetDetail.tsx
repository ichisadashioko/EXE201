import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router"
import { api_get_pet_info, api_upload_pet_image, getAccessToken } from "../authentication";

import './PetDetail.css';
import type { PetImageInfo } from "../typing";

interface PetPicture {
    id: number;
    url: string;
    created_at: string;
}

interface Pet {
    id: number;
    name: string;
    description: string;
    owner_id: number;
    can_edit: boolean;
    profile_image_id: number | null;
    profile_image_url: string | null;
    images: PetPicture[]; // Add this to your Pet interface
}

export default function PetDetail() {
    const { petId } = useParams<{ petId: string }>();
    const navigate = useNavigate();
    const access_token = getAccessToken();

    const [image_list, setImageList] = useState<PetImageInfo[]>([]);

    //

    const [pet, setPet] = useState<Pet | null>(null); // Use the updated Pet interface
    const [isUpdating, setIsUpdating] = useState(false); // To disable buttons during API calls

    // Function to handle setting the profile picture
    const handleSetProfilePicture = async (pictureId: number) => {
        if (!pet || !access_token) return;

        setIsUpdating(true);

        try {
            // /api/pets/{pet_id}/set_profile_image/{image_id}
            // const response = await fetch(`/api/pets/${pet.id}/profile-picture`, {
            const response = await fetch(`/api/pets/${pet.id}/set_profile_image/${pictureId}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${access_token}`,
                },
                // body: JSON.stringify({ pictureId: pictureId }),
            });

            if (response.ok) {
                // For a better user experience, update the local state immediately
                // instead of re-fetching the entire pet object.
                const newProfilePicture = pet.images.find(img => img.id === pictureId);
                if (newProfilePicture) {
                    setPet(prevPet => {
                        if (!prevPet) return null;
                        return {
                            ...prevPet,
                            profile_image_id: newProfilePicture.id,
                            profile_image_url: newProfilePicture.url,
                        };
                    });
                }
                alert('Profile picture updated!');
            } else {
                const errorData = await response.json();
                alert(`Failed to update profile picture: ${errorData.message}`);
            }
        } catch (error) {
            console.error("Error setting profile picture:", error);
            alert("An error occurred while updating the profile picture.");
        } finally {
            setIsUpdating(false);
        }
    };

    const upload_file = async (file_obj: File) => {
        let retval = await api_upload_pet_image(
            access_token!,
            petId!,
            file_obj,
        );

        console.debug(retval);

        if (retval.success) {
            try {
                let image_info: PetImageInfo = retval.data.image_info;
                // let image_url = retval.data.image_info.url;
                console.log("Uploaded image URL:", image_info.url);
                // append to image list in UI
                setImageList([...image_list, image_info]);
            } catch (error) {
                console.error("Error uploading image:");
                console.error(error);
                alert(`Error uploading image: ${error}`);
                return;
            }
            // alert("Image uploaded successfully");
        } else {
            alert(`Failed to upload image: ${retval.message}`);
        }
    }

    useEffect(() => {
        if (access_token == null) {
            console.log("Access token is null, redirecting to login page");
            navigate("/login");
        }

        if (petId == null) {
            console.error("petId is null, redirecting to home page");
            navigate("/home");
        }

        if ((access_token != null) && (petId != null)) {
            const fetchPetDetails = async () => {
                try {
                    const retval = await api_get_pet_info(
                        access_token,
                        petId,
                    );

                    console.debug(retval);

                    if (retval.success) {
                        console.log("Pet info:", retval.data);
                        // TODO display pet info
                        // alert(`Pet info: ${JSON.stringify(retval.data)}`);
                        try {
                            setPet(retval.data);
                            setImageList(retval.data.images);
                        } catch (error) {
                            console.error("Error fetching pet info:");
                            console.error(error);
                            alert(`Error fetching pet info: ${error}`);
                        }
                    } else {
                        console.error("Failed to get pet info:", retval.message);
                        alert(`Failed to get pet info: ${retval.message}`);
                        // navigate("/home");
                    }
                } catch (error) {
                    console.error("Error fetching pet info:");
                    console.error(error);
                    alert(`Error fetching pet info: ${error}`);
                    // navigate("/home");
                }
            };

            fetchPetDetails();
        }
    }, []);

    if (!pet) {
        return <div>Pet not found.</div>;
    }

    return (
        <div>
            <h1>{pet.name}</h1>
            {pet.profile_image_url && (
                <div>
                    <h3>Profile Picture</h3>
                    <img src={pet.profile_image_url} alt="Profile" style={{ maxWidth: '200px', maxHeight: '200px' }} />
                </div>
            )}
            <p>{pet.description}</p>
            <div>
                <h2>All Images</h2>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '10px' }}>
                    {pet.can_edit ? (
                        <div>
                            Can edit
                        </div>
                    ) : null}
                    {pet.images.map((pic) => (
                        <div key={pic.id} style={{ border: '1px solid #ccc', padding: '5px' }}>
                            <img src={pic.url} alt={`Pet image ${pic.id}`} style={{ width: '100px', height: '100px', objectFit: 'cover' }} />
                            {pet.profile_image_id === pic.id ? (
                                <strong>(Profile Picture)</strong>
                            ) : (
                                pet.can_edit ? (
                                    <button
                                        onClick={() => handleSetProfilePicture(pic.id)}
                                        disabled={isUpdating}
                                    >
                                        Set as Profile
                                    </button>
                                ) : null
                            )}
                        </div>
                    ))}
                </div>
            </div>

            <div id="pet_images">
                <h2>Images:</h2>
                <div>
                    {image_list.map((image_info) => (
                        <div key={image_info.id} className="pet_image_item">
                            <img src={image_info.url} alt={`Pet Image ${image_info.id}`} className="pet_image" />
                            {/* <p>Uploaded at: {new Date(image_info.created_ts * 1000).toLocaleString()}</p> */}
                            <p>Uploaded at: {image_info.created_ts}</p>
                        </div>
                    ))}
                </div>
            </div>
            <div>
                <h2>TODO: upload images</h2>
            </div>
            <div>
                <div>
                    <input
                        type='file'
                        accept="image/jpeg, image/png"
                        id="input_upload_image"
                        onChange={(evt) => {
                            console.log("Selected files:", evt.target.files);
                            if (evt.target.files == null) {
                                return;
                            }
                            if (evt.target.files.length == 0) {
                                return;
                            }

                            let file_obj = evt.target.files[0];
                            if (file_obj == null) {
                                return;
                            }

                            console.log("Selected file:", file_obj);
                            upload_file(file_obj);
                            // api_upload_pet_image(
                            //     access_token!,
                            //     petId!,
                            //     file_obj,
                            // ).then((response) => {
                            //     console.log("Upload image response:", response);
                            //     if (response.success) {
                            //         alert("Image uploaded successfully");
                            //     } else {
                            //         alert(`Failed to upload image: ${response.message}`);
                            //     }
                            // });
                        }}
                    />
                </div>
                <div>
                    <button onClick={() => {
                        let input_elem = (document.getElementById("input_upload_image") as HTMLInputElement);
                        if (input_elem == null) {
                            console.error("input_upload_image element not found");
                            alert("input_upload_image element not found");
                            return;
                        }
                        input_elem.click();
                    }}>upload images</button>
                </div>

            </div>
        </div>
    )
}
