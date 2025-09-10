import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router"
import { api_get_pet_info, api_upload_pet_image, getAccessToken } from "../authentication";

import './PetDetail.css';

interface PetImageInfo {
    id: string;
    url: string;
    created_ts: number;
}

export default function PetDetail() {
    const { petId } = useParams<{ petId: string }>();
    const navigate = useNavigate();
    const access_token = getAccessToken();

    const [name, setName] = useState<string>("");
    const [description, setDescription] = useState<string>("");
    const [image_list, setImageList] = useState<PetImageInfo[]>([]);

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
                            let pet_name = retval.data.name;
                            let description = retval.data.description;
                            setName(pet_name);
                            setDescription(description);
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

    return (
        <div>
            <h1>Pet Detail Page - TODO</h1>
            <div>
                <h3>Name:</h3>
                <p className="red">{name}</p>
            </div>
            <div>
                <h3>Description:</h3>
                <p>{description}</p>
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
                    }}>upload images</button>
                </div>

            </div>
        </div>
    )
}
