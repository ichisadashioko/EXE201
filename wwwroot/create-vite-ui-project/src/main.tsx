// import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { BrowserRouter, Routes, Route } from 'react-router';
import Login from './Login.tsx';
import Signup from './Signup.tsx';
import Home from './users/Home.tsx';
import NewPet from './pets/NewPet.tsx';
import PetDetail from './pets/PetDetail.tsx';
// import { HOME_ROUTE } from './route_config.ts';

createRoot(document.getElementById('root')!).render(
  <BrowserRouter>
    <Routes>
      {/* <Route path=HOME_ROUTE element={<App />} /> */}
      <Route path="/" element={<App />} />
      <Route path="/index.html" element={<App />} />
      <Route path="/login" element={<Login />} />
      <Route path="/signup" element={<Signup />} />
      <Route path="/home" element={<Home />} />
      <Route path="/pets/create" element={<NewPet />} />
      <Route path="/pets/:petId" element={<PetDetail />} />
    </Routes>
  </BrowserRouter>,
)
